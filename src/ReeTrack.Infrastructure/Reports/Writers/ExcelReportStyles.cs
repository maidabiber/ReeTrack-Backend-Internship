using ClosedXML.Excel;

namespace ReeTrack.Infrastructure.Reports.Writers;

/// <summary>
/// Shared ClosedXML styling toolkit. Originally private to <c>ExcelReportWriter</c>
/// (the Summary export, which shipped first) — the three newer writers
/// (Detailed/Workload/Profitability) each re-implemented their own overview sheet from
/// scratch instead of reusing this, so those sheets ended up unstyled. Promoted here so
/// they can call the same styling instead of duplicating (or skipping) it.
/// </summary>
internal static class ExcelReportStyles
{
    public static void AddDataBars(IXLRange range)
    {
        // ClosedXML data bars = the chart substitute. Brand fill, show cell values.
        range.AddConditionalFormat()
            .DataBar(XLColor.FromHtml(ReportColors.Brand), true)
            .Minimum(XLCFContentType.Number, 0)
            .Maximum(XLCFContentType.Maximum, 0);
    }

    public static string CurrencyFormat(string currencyCode)
    {
        var code = string.IsNullOrWhiteSpace(currencyCode)
            ? ""
            : currencyCode.Trim().ToUpperInvariant();
        return string.IsNullOrEmpty(code)
            ? "#,##0.00"
            : $"#,##0.00 \"{code}\"";
    }

    public static void StyleTitle(IXLCell cell)
    {
        cell.Style.Font.Bold = true;
        cell.Style.Font.FontSize = 16;
        cell.Style.Font.FontColor = XLColor.FromHtml(ReportColors.Navy);
    }

    public static void StyleHeaderBand(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.FromHtml(ReportColors.HeaderGray);
        range.Style.Fill.BackgroundColor = XLColor.FromHtml(ReportColors.HeaderGrayBg);
        range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    public static void StyleMuted(IXLCell cell) =>
        cell.Style.Font.FontColor = XLColor.FromHtml(ReportColors.Gray);

    public static void Zebra(IXLRange range) =>
        range.Style.Fill.BackgroundColor = XLColor.FromHtml(ReportColors.SurfaceMuted);

    /// <summary>A grouped table's section header row — bold, no fill, so it reads as a label, not data.</summary>
    public static void StyleGroupHeader(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Font.FontColor = XLColor.FromHtml(ReportColors.Navy);
    }

    /// <summary>A grouped table's subtotal row — bold and shaded, distinct from the zebra stripe on detail rows.</summary>
    public static void StyleGroupSubtotal(IXLRange range)
    {
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml(ReportColors.HeaderGrayBg);
    }
}
