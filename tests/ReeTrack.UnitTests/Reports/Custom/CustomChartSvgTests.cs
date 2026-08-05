using System.Globalization;
using ReeTrack.Application.Common.Models.CustomReports;
using ReeTrack.Infrastructure.Reports.Writers;
using ReeTrack.Infrastructure.Reports.Writers.Custom;
using Xunit;

namespace ReeTrack.UnitTests.Reports.Custom;

public class CustomChartSvgTests
{
    private static SeriesResult Series(
        ChartKind kind,
        IReadOnlyList<string> categories,
        params NamedSeries[] series) =>
        new()
        {
            Id = "b1",
            Kind = kind,
            Categories = categories,
            Series = series
        };

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void Render_NoCategoriesOrNoSeries_ReturnsNull(int categoryCount, int seriesCount)
    {
        var categories = Enumerable.Range(0, categoryCount).Select(i => $"c{i}").ToArray();
        var series = Enumerable.Range(0, seriesCount)
            .Select(i => new NamedSeries { Key = $"s{i}", Label = $"S{i}", Values = [1m] })
            .ToArray();

        var svg = CustomChartSvg.Render(Series(ChartKind.Bar, categories, series));

        Assert.Null(svg);
    }

    [Fact]
    public void Render_Bar_EmitsOneRectPerSeriesPerCategoryPlusLegendSwatches()
    {
        var model = Series(
            ChartKind.Bar,
            ["Jan", "Feb"],
            new NamedSeries { Key = "a", Label = "A", Values = [1m, 2m] },
            new NamedSeries { Key = "b", Label = "B", Values = [3m, 4m] });

        var svg = CustomChartSvg.Render(model);

        Assert.NotNull(svg);
        Assert.StartsWith("<svg", svg);
        // 2 categories * 2 series bars + 2 legend swatches (multi-series legend)
        Assert.Equal(6, Count(svg!, "<rect "));
        Assert.Contains(">Jan<", svg);
        Assert.Contains(">Feb<", svg);
        Assert.Contains(">A<", svg);
        Assert.Contains(">B<", svg);
    }

    [Fact]
    public void Render_Line_EmitsOnePolylinePerSeriesAndNoFilledPaths()
    {
        var model = Series(
            ChartKind.Line,
            ["Jan", "Feb", "Mar"],
            new NamedSeries { Key = "a", Label = "A", Values = [1m, 2m, 3m] },
            new NamedSeries { Key = "b", Label = "B", Values = [3m, 2m, 1m] });

        var svg = CustomChartSvg.Render(model);

        Assert.NotNull(svg);
        Assert.Equal(2, Count(svg!, "<polyline "));
        Assert.DoesNotContain("<path ", svg);
    }

    [Fact]
    public void Render_Area_ClosesEachSeriesToBaselineWithBrandGradientOnFirstSeries()
    {
        var model = Series(
            ChartKind.Area,
            ["Jan", "Feb", "Mar"],
            new NamedSeries { Key = "a", Label = "A", Values = [1m, 2m, 3m] },
            new NamedSeries { Key = "b", Label = "B", Values = [3m, 2m, 1m] });

        var svg = CustomChartSvg.Render(model);

        Assert.NotNull(svg);
        // One filled, baseline-closed path per series, plus the stroke polyline per series.
        Assert.Equal(2, Count(svg!, "<path "));
        Assert.Equal(2, Count(svg!, "<polyline "));
        Assert.Contains("linearGradient", svg);
        Assert.Contains("url(#areaFill)", svg);
        Assert.Contains("url(#areaStroke)", svg);
        // Second series falls back to a flat series color instead of the gradient.
        Assert.Contains($"fill=\"{ReportColors.SeriesAt(1)}\"", svg);
    }

    [Fact]
    public void Render_Donut_UsesFirstSeriesOnlyAndDrawsArcsAt55To85PercentRadius()
    {
        var model = Series(
            ChartKind.Donut,
            ["A", "B"],
            new NamedSeries { Key = "primary", Label = "Primary", Values = [1m, 2m] },
            new NamedSeries { Key = "ignored", Label = "Ignored", Values = [99m, 99m] });

        var svg = CustomChartSvg.Render(model);

        Assert.NotNull(svg);
        // One donut wedge per category with a positive value in the first series only.
        Assert.Equal(2, Count(svg!, "<path "));
        // Radii come from ViewHeight/2 (110) minus a 10pt margin (100), scaled 55%/85% —
        // matching ChartBlockView's innerRadius="55%" outerRadius="85%".
        Assert.Contains("A 85 85 0", svg);
        Assert.Contains("A 55 55 0", svg);
        Assert.Contains($"fill=\"{ReportColors.SeriesAt(0)}\"", svg);
        Assert.Contains($"fill=\"{ReportColors.SeriesAt(1)}\"", svg);
        // The "Ignored" series never contributes a slice or a legend entry.
        Assert.DoesNotContain(">Ignored<", svg);
        Assert.Contains(">A<", svg);
        Assert.Contains(">B<", svg);
    }

    [Fact]
    public void Render_EscapesXmlSpecialCharactersInCategoryAndSeriesLabels()
    {
        var model = Series(
            ChartKind.Bar,
            ["A & B <script>"],
            new NamedSeries { Key = "a", Label = "Q&A \"quoted\"", Values = [1m] },
            new NamedSeries { Key = "b", Label = "Second", Values = [2m] });

        var svg = CustomChartSvg.Render(model);

        Assert.NotNull(svg);
        Assert.DoesNotContain("<script>", svg);
        Assert.DoesNotContain("A & B", svg);
        Assert.Contains("&amp;", svg);
        Assert.Contains("&lt;script&gt;", svg);
        Assert.Contains("&quot;quoted&quot;", svg);
    }

    [Fact]
    public void Render_UsesInvariantCultureForCoordinates_RegardlessOfThreadCulture()
    {
        // Regression guard: a comma-decimal server locale must not leak into SVG numeric
        // attributes (the same bug class this codebase has previously shipped for exports).
        var model = Series(
            ChartKind.Bar,
            ["A", "B", "C"],
            new NamedSeries { Key = "a", Label = "A", Values = [1m, 2m, 3m] });

        var originalCulture = Thread.CurrentThread.CurrentCulture;
        Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        string? svg;
        try
        {
            svg = CustomChartSvg.Render(model);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
        }

        Assert.NotNull(svg);
        // y for the first bar (value 1 of 0..3, plot spans y=10..194) is 132.66666... -> 132.667.
        Assert.Contains("132.667", svg);
        Assert.DoesNotContain("132,667", svg);
    }

    private static int Count(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
