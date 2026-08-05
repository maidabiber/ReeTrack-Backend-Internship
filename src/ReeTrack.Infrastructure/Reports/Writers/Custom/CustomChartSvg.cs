using System.Globalization;
using System.Text;
using ReeTrack.Application.Common.Models.CustomReports;

namespace ReeTrack.Infrastructure.Reports.Writers.Custom;

/// <summary>
/// Renders a <see cref="SeriesResult"/> as a self-contained SVG string for embedding in the
/// PDF export via QuestPDF's <c>Svg</c> element. Mirrors the on-screen Recharts rendering in
/// the frontend's ChartBlockView closely enough to be recognizable as the same chart, without
/// pulling a charting library onto the server.
/// </summary>
public static class CustomChartSvg
{
    private const double ViewWidth = 600;
    private const double ViewHeight = 220;

    /// <summary>
    /// Returns <c>null</c> when there is nothing to draw, so the caller can fall back to its
    /// own "No series data." text instead of emitting a degenerate SVG.
    /// </summary>
    public static string? Render(SeriesResult series)
    {
        if (series.Categories.Count == 0 || series.Series.Count == 0)
            return null;

        return series.Kind switch
        {
            ChartKind.Bar => RenderBar(series),
            ChartKind.Line => RenderLine(series, area: false),
            ChartKind.Area => RenderLine(series, area: true),
            ChartKind.Donut => RenderDonut(series),
            _ => null
        };
    }

    private static string RenderBar(SeriesResult series)
    {
        var (x0, x1, y0, y1) = PlotArea(withLegend: series.Series.Count > 1);
        var (min, max) = ValueRange(series.Series);
        var baselineY = ScaleY(0m, min, max, y0, y1);

        var categoryCount = series.Categories.Count;
        var groupWidth = (x1 - x0) / categoryCount;
        var groupPadding = groupWidth * 0.12;
        var innerWidth = groupWidth - 2 * groupPadding;
        var seriesCount = series.Series.Count;
        var barGap = seriesCount > 1 ? innerWidth * 0.08 : 0;
        var barWidth = Math.Max(1, (innerWidth - barGap * (seriesCount - 1)) / seriesCount);

        var sb = new StringBuilder();
        AppendHeader(sb);
        AppendBaseline(sb, x0, x1, baselineY);

        for (var c = 0; c < categoryCount; c++)
        {
            var groupX = x0 + c * groupWidth + groupPadding;
            for (var s = 0; s < seriesCount; s++)
            {
                var value = ValueAt(series.Series[s], c);
                var barX = groupX + s * (barWidth + barGap);
                var valueY = ScaleY(value, min, max, y0, y1);
                var rectY = Math.Min(valueY, baselineY);
                var rectH = Math.Max(0, Math.Abs(valueY - baselineY));
                sb.Append(
                    $"<rect x=\"{F(barX)}\" y=\"{F(rectY)}\" width=\"{F(barWidth)}\" height=\"{F(rectH)}\" fill=\"{ReportColors.SeriesAt(s)}\" rx=\"1.5\" />");
            }

            AppendCategoryLabel(sb, groupX + innerWidth / 2, y1, series.Categories[c]);
        }

        if (seriesCount > 1)
            AppendSeriesLegend(sb, series.Series);

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string RenderLine(SeriesResult series, bool area)
    {
        var (x0, x1, y0, y1) = PlotArea(withLegend: series.Series.Count > 1);
        var (min, max) = ValueRange(series.Series);
        var baselineY = ScaleY(0m, min, max, y0, y1);
        var categoryCount = series.Categories.Count;
        var step = categoryCount > 1 ? (x1 - x0) / (categoryCount - 1) : 0;

        var sb = new StringBuilder();
        AppendHeader(sb);
        if (area)
            AppendAreaGradientDefs(sb);
        AppendBaseline(sb, x0, x1, baselineY);

        for (var s = 0; s < series.Series.Count; s++)
        {
            var points = new (double X, double Y)[categoryCount];
            for (var c = 0; c < categoryCount; c++)
            {
                var value = ValueAt(series.Series[s], c);
                var x = categoryCount > 1 ? x0 + c * step : (x0 + x1) / 2;
                points[c] = (x, ScaleY(value, min, max, y0, y1));
            }

            if (area)
            {
                // Line path closed down to the baseline so it can be filled — the frontend's
                // Area block does the same (draws the line, then closes to zero for the fill).
                var pathPoints = string.Join(" L ", points.Select(p => $"{F(p.X)} {F(p.Y)}"));
                var fillRef = s == 0 ? "url(#areaFill)" : ReportColors.SeriesAt(s);
                var fillOpacity = s == 0 ? "1" : "0.12";
                sb.Append(
                    $"<path d=\"M {pathPoints} L {F(points[^1].X)} {F(baselineY)} L {F(points[0].X)} {F(baselineY)} Z\" " +
                    $"fill=\"{fillRef}\" fill-opacity=\"{fillOpacity}\" stroke=\"none\" />");
            }

            var strokeRef = area && s == 0 ? "url(#areaStroke)" : ReportColors.SeriesAt(s);
            var pointsAttr = string.Join(" ", points.Select(p => $"{F(p.X)},{F(p.Y)}"));
            sb.Append(
                $"<polyline points=\"{pointsAttr}\" fill=\"none\" stroke=\"{strokeRef}\" " +
                "stroke-width=\"2\" stroke-linejoin=\"round\" stroke-linecap=\"round\" />");
        }

        for (var c = 0; c < categoryCount; c++)
        {
            var x = categoryCount > 1 ? x0 + c * step : (x0 + x1) / 2;
            AppendCategoryLabel(sb, x, y1, series.Categories[c]);
        }

        if (series.Series.Count > 1)
            AppendSeriesLegend(sb, series.Series);

        sb.Append("</svg>");
        return sb.ToString();
    }

    private static string RenderDonut(SeriesResult series)
    {
        // Recharts' Pie shows one value per category from a single dataKey — matched here by
        // reading only the first series and slicing by category, exactly like ChartBlockView.
        var primary = series.Series[0];
        var values = new decimal[series.Categories.Count];
        for (var c = 0; c < values.Length; c++)
            values[c] = Math.Max(0m, ValueAt(primary, c));
        var total = values.Sum();

        const double legendWidth = ViewWidth * 0.38;
        const double donutAreaWidth = ViewWidth - legendWidth;
        const double cx = donutAreaWidth / 2;
        const double cy = ViewHeight / 2;
        var baseRadius = Math.Min(cx, cy) - 10;
        var outerR = baseRadius * 0.85;
        var innerR = baseRadius * 0.55;

        var sb = new StringBuilder();
        AppendHeader(sb);

        if (total <= 0m)
        {
            sb.Append(
                $"<circle cx=\"{F(cx)}\" cy=\"{F(cy)}\" r=\"{F((outerR + innerR) / 2)}\" fill=\"none\" " +
                $"stroke=\"{ReportColors.SurfaceMuted}\" stroke-width=\"{F(outerR - innerR)}\" />");
        }
        else
        {
            double startAngle = 0;
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i] <= 0m)
                    continue;

                // Clamp below a full circle: a 0°→360° arc is degenerate (start == end point),
                // which happens whenever one category holds the entire total.
                var sweep = Math.Min(359.99, (double)(values[i] / total) * 360.0);
                var endAngle = startAngle + sweep;
                sb.Append(
                    $"<path d=\"{DonutSlicePath(cx, cy, outerR, innerR, startAngle, endAngle)}\" fill=\"{ReportColors.SeriesAt(i)}\" />");
                startAngle = endAngle;
            }
        }

        AppendDonutLegend(sb, donutAreaWidth + 12, series.Categories);

        sb.Append("</svg>");
        return sb.ToString();
    }

    // ---- shared geometry helpers ----

    private static decimal ValueAt(NamedSeries series, int categoryIndex) =>
        categoryIndex < series.Values.Count ? series.Values[categoryIndex] : 0m;

    private static (double X0, double X1, double Y0, double Y1) PlotArea(bool withLegend)
    {
        const double left = 10;
        const double right = 10;
        const double bottom = 26;
        var top = withLegend ? 26d : 10d;
        return (left, ViewWidth - right, top, ViewHeight - bottom);
    }

    private static (decimal Min, decimal Max) ValueRange(IReadOnlyList<NamedSeries> series)
    {
        var all = series.SelectMany(s => s.Values).ToArray();
        var max = Math.Max(0m, all.Length > 0 ? all.Max() : 0m);
        var min = Math.Min(0m, all.Length > 0 ? all.Min() : 0m);
        if (max == min)
            max = min + 1m; // all-zero series — keep the scale non-degenerate
        return (min, max);
    }

    private static double ScaleY(decimal value, decimal min, decimal max, double y0, double y1)
    {
        var ratio = (double)((value - min) / (max - min));
        return y1 - ratio * (y1 - y0);
    }

    private static (double X, double Y) PointOnCircle(double cx, double cy, double r, double angleDeg)
    {
        var rad = (angleDeg - 90) * Math.PI / 180.0; // 0° = top, clockwise
        return (cx + r * Math.Cos(rad), cy + r * Math.Sin(rad));
    }

    private static string DonutSlicePath(
        double cx, double cy, double outerR, double innerR, double startDeg, double endDeg)
    {
        var largeArc = endDeg - startDeg > 180 ? 1 : 0;
        var outerStart = PointOnCircle(cx, cy, outerR, startDeg);
        var outerEnd = PointOnCircle(cx, cy, outerR, endDeg);
        var innerStart = PointOnCircle(cx, cy, innerR, startDeg);
        var innerEnd = PointOnCircle(cx, cy, innerR, endDeg);

        return
            $"M {F(outerStart.X)} {F(outerStart.Y)} " +
            $"A {F(outerR)} {F(outerR)} 0 {largeArc} 1 {F(outerEnd.X)} {F(outerEnd.Y)} " +
            $"L {F(innerEnd.X)} {F(innerEnd.Y)} " +
            $"A {F(innerR)} {F(innerR)} 0 {largeArc} 0 {F(innerStart.X)} {F(innerStart.Y)} Z";
    }

    // ---- markup helpers ----

    private static void AppendHeader(StringBuilder sb) =>
        sb.Append($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {F(ViewWidth)} {F(ViewHeight)}\">");

    private static void AppendBaseline(StringBuilder sb, double x0, double x1, double baselineY) =>
        sb.Append(
            $"<line x1=\"{F(x0)}\" y1=\"{F(baselineY)}\" x2=\"{F(x1)}\" y2=\"{F(baselineY)}\" " +
            $"stroke=\"{ReportColors.SurfaceMuted}\" stroke-width=\"1\" />");

    private static void AppendCategoryLabel(StringBuilder sb, double x, double axisY, string label) =>
        sb.Append(
            $"<text x=\"{F(x)}\" y=\"{F(axisY + 14)}\" font-size=\"8\" fill=\"{ReportColors.NavyMuted}\" " +
            $"text-anchor=\"middle\">{Escape(Truncate(label))}</text>");

    private static void AppendSeriesLegend(StringBuilder sb, IReadOnlyList<NamedSeries> series)
    {
        var x = 10d;
        const double y = 12;
        for (var i = 0; i < series.Count; i++)
        {
            var label = Truncate(series[i].Label);
            sb.Append(
                $"<rect x=\"{F(x)}\" y=\"{F(y - 7)}\" width=\"8\" height=\"8\" fill=\"{ReportColors.SeriesAt(i)}\" rx=\"1.5\" />" +
                $"<text x=\"{F(x + 11)}\" y=\"{F(y)}\" font-size=\"8\" fill=\"{ReportColors.Navy}\">{Escape(label)}</text>");
            x += 11 + label.Length * 4.6 + 14; // rough advance — no server-side text metrics available
        }
    }

    private static void AppendDonutLegend(StringBuilder sb, double x, IReadOnlyList<string> categories)
    {
        var y = 16d;
        for (var i = 0; i < categories.Count; i++)
        {
            sb.Append(
                $"<rect x=\"{F(x)}\" y=\"{F(y - 7)}\" width=\"8\" height=\"8\" fill=\"{ReportColors.SeriesAt(i)}\" rx=\"1.5\" />" +
                $"<text x=\"{F(x + 11)}\" y=\"{F(y)}\" font-size=\"8\" fill=\"{ReportColors.Navy}\">{Escape(Truncate(categories[i]))}</text>");
            y += 14;
            if (y > ViewHeight - 8)
                break; // more categories than fit — the rest stay in the arcs, just unlabeled
        }
    }

    private static string Truncate(string value) =>
        value.Length > 18 ? string.Concat(value.AsSpan(0, 17), "…") : value;

    private static string Escape(string value) =>
        value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");

    private static void AppendAreaGradientDefs(StringBuilder sb) =>
        sb.Append(
            "<defs>" +
            $"<linearGradient id=\"areaStroke\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"1\">" +
            $"<stop offset=\"0%\" stop-color=\"{ReportColors.Brand}\" />" +
            $"<stop offset=\"100%\" stop-color=\"{ReportColors.BrandHi}\" />" +
            "</linearGradient>" +
            $"<linearGradient id=\"areaFill\" x1=\"0\" y1=\"0\" x2=\"0\" y2=\"1\">" +
            $"<stop offset=\"0%\" stop-color=\"{ReportColors.Brand}\" stop-opacity=\"0.16\" />" +
            $"<stop offset=\"70%\" stop-color=\"{ReportColors.BrandHi}\" stop-opacity=\"0.05\" />" +
            $"<stop offset=\"100%\" stop-color=\"{ReportColors.BrandHi}\" stop-opacity=\"0\" />" +
            "</linearGradient>" +
            "</defs>");

    /// <summary>Exports must not shift decimal separators with the server's locale — every
    /// numeric literal in the emitted SVG goes through this, mirroring the invariant-culture
    /// convention CustomPdfReportWriter uses for its own Number(decimal) formatting.</summary>
    private static string F(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}
