using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using ReeTrack.Application.Common.Models.CustomReports;
using A = DocumentFormat.OpenXml.Drawing;
using C = DocumentFormat.OpenXml.Drawing.Charts;
using Xdr = DocumentFormat.OpenXml.Drawing.Spreadsheet;
using X = DocumentFormat.OpenXml.Spreadsheet;

namespace ReeTrack.Infrastructure.Reports.Writers.Custom;

/// <summary>
/// Post-processes a workbook <see cref="CustomExcelReportWriter"/> already saved with
/// ClosedXML, injecting one native Excel chart per data sheet written from a
/// <see cref="SeriesResult"/>. ClosedXML 0.105 has no chart API, so this reopens the saved
/// package directly with <c>DocumentFormat.OpenXml</c> — safe to do because ClosedXML itself
/// is built on the same library (it depends on <c>DocumentFormat.OpenXml</c> 3.1.1 to read
/// and write the .xlsx package), so re-parsing its output with the same library is exactly
/// what ClosedXML does internally on every load.
/// </summary>
internal static class ExcelChartInjector
{
    /// <summary>
    /// The sheet name plus the data extent <see cref="CustomExcelReportWriter"/> wrote for a
    /// <see cref="SeriesResult"/> block, so the injector can build range references without
    /// re-deriving them from a <see cref="SeriesResult"/> it never sees. Category values live
    /// in column A rows 2..(1+CategoryCount); series values start at column B.
    /// </summary>
    public readonly record struct ChartPlacement(string SheetName, ChartKind Kind, int CategoryCount, int SeriesCount);

    // Arbitrary but stable — only need to be unique within a chart's own axis pair.
    private const uint CategoryAxisId = 111111111;
    private const uint ValueAxisId = 222222222;

    public static void Inject(Stream stream, IReadOnlyList<ChartPlacement> placements)
    {
        if (placements.Count == 0)
            return;

        stream.Position = 0;
        using var document = SpreadsheetDocument.Open(stream, true);
        var workbookPart = document.WorkbookPart!;
        var sheets = workbookPart.Workbook.Sheets!;

        foreach (var placement in placements)
        {
            // A SeriesResult with no categories or no series writes a header-only sheet.
            // Injecting a chart over an empty range produces a workbook Excel reports as
            // corrupt, so this skip is load-bearing, not optional polish.
            if (placement.CategoryCount == 0 || placement.SeriesCount == 0)
                continue;

            var sheetElement = sheets.Elements<X.Sheet>().FirstOrDefault(s => s.Name == placement.SheetName);
            if (sheetElement?.Id?.Value is not { } relationshipId)
                continue;
            if (workbookPart.GetPartById(relationshipId) is not WorksheetPart worksheetPart)
                continue;

            InjectChart(worksheetPart, placement);
        }
    }

    private static void InjectChart(WorksheetPart worksheetPart, ChartPlacement placement)
    {
        var drawingsPart = worksheetPart.AddNewPart<DrawingsPart>();
        var chartPart = drawingsPart.AddNewPart<ChartPart>();
        chartPart.ChartSpace = new C.ChartSpace(BuildChart(placement));
        chartPart.ChartSpace.Save();

        var chartRelationshipId = drawingsPart.GetIdOfPart(chartPart);
        drawingsPart.WorksheetDrawing = new Xdr.WorksheetDrawing(BuildAnchor(chartRelationshipId, placement.SeriesCount));
        drawingsPart.WorksheetDrawing.Save();

        AppendDrawingReference(worksheetPart, drawingsPart);
    }

    // ---- chart XML ----

    private static C.Chart BuildChart(ChartPlacement placement)
    {
        var quotedSheet = QuoteSheetName(placement.SheetName);
        var plotAreaChildren = new List<OpenXmlElement>
        {
            new C.Layout(),
            BuildTypedChart(placement, quotedSheet)
        };

        // Pie-family charts (doughnut included) have no axes in the OOXML chart model —
        // DoughnutChart has no AxisId children, unlike Bar/Line/Area — so skip them here
        // rather than emitting axes the chart never references.
        if (placement.Kind != ChartKind.Donut)
        {
            plotAreaChildren.Add(new C.CategoryAxis(
                new C.AxisId { Val = CategoryAxisId },
                new C.Scaling(new C.Orientation { Val = C.OrientationValues.MinMax }),
                new C.Delete { Val = false },
                new C.AxisPosition { Val = C.AxisPositionValues.Bottom },
                new C.CrossingAxis { Val = ValueAxisId }));
            plotAreaChildren.Add(new C.ValueAxis(
                new C.AxisId { Val = ValueAxisId },
                new C.Scaling(new C.Orientation { Val = C.OrientationValues.MinMax }),
                new C.Delete { Val = false },
                new C.AxisPosition { Val = C.AxisPositionValues.Left },
                new C.CrossingAxis { Val = CategoryAxisId }));
        }

        return new C.Chart(
            new C.AutoTitleDeleted { Val = true },
            new C.PlotArea(plotAreaChildren),
            new C.Legend(new C.LegendPosition { Val = C.LegendPositionValues.Bottom }),
            new C.PlotVisibleOnly { Val = true });
    }

    private static OpenXmlCompositeElement BuildTypedChart(ChartPlacement placement, string quotedSheet)
    {
        if (placement.Kind == ChartKind.Donut)
            return BuildDoughnutChart(placement, quotedSheet);

        OpenXmlCompositeElement chart = placement.Kind switch
        {
            ChartKind.Bar => new C.BarChart(
                new C.BarDirection { Val = C.BarDirectionValues.Column },
                new C.BarGrouping { Val = C.BarGroupingValues.Clustered },
                new C.VaryColors { Val = false }),
            ChartKind.Line => new C.LineChart(
                new C.Grouping { Val = C.GroupingValues.Standard },
                new C.VaryColors { Val = false }),
            ChartKind.Area => new C.AreaChart(
                new C.Grouping { Val = C.GroupingValues.Standard },
                new C.VaryColors { Val = false }),
            _ => throw new ArgumentOutOfRangeException(nameof(placement), placement.Kind, "Unsupported chart kind.")
        };

        for (var s = 0; s < placement.SeriesCount; s++)
            chart.Append(BuildCartesianSeries(placement.Kind, quotedSheet, placement.CategoryCount, s));

        chart.Append(new C.AxisId { Val = CategoryAxisId }, new C.AxisId { Val = ValueAxisId });
        return chart;
    }

    private static OpenXmlElement BuildCartesianSeries(ChartKind kind, string quotedSheet, int categoryCount, int seriesIndex)
    {
        var seriesColumn = ColumnLetter(seriesIndex + 2); // column A is categories; series start at B
        var categoryAxisData = BuildCategoryAxisData(quotedSheet, categoryCount);
        var values = BuildValues(quotedSheet, seriesColumn, categoryCount);
        var seriesText = BuildSeriesText(quotedSheet, seriesColumn);
        var colorHex = ToHex(ReportColors.SeriesAt(seriesIndex));

        return kind switch
        {
            ChartKind.Bar => new C.BarChartSeries(
                new C.Index { Val = (uint)seriesIndex }, new C.Order { Val = (uint)seriesIndex }, seriesText,
                new C.ChartShapeProperties(new A.SolidFill(new A.RgbColorModelHex { Val = colorHex })),
                categoryAxisData, values),
            ChartKind.Line => new C.LineChartSeries(
                new C.Index { Val = (uint)seriesIndex }, new C.Order { Val = (uint)seriesIndex }, seriesText,
                new C.ChartShapeProperties(new A.Outline(new A.SolidFill(new A.RgbColorModelHex { Val = colorHex }))),
                new C.Marker(new C.Symbol { Val = C.MarkerStyleValues.None }),
                categoryAxisData, values),
            ChartKind.Area => new C.AreaChartSeries(
                new C.Index { Val = (uint)seriesIndex }, new C.Order { Val = (uint)seriesIndex }, seriesText,
                new C.ChartShapeProperties(new A.SolidFill(new A.RgbColorModelHex { Val = colorHex })),
                categoryAxisData, values),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported cartesian chart kind.")
        };
    }

    private static C.DoughnutChart BuildDoughnutChart(ChartPlacement placement, string quotedSheet)
    {
        var doughnut = new C.DoughnutChart(new C.VaryColors { Val = true });
        for (var s = 0; s < placement.SeriesCount; s++)
            doughnut.Append(BuildPieSeries(placement, quotedSheet, s));
        doughnut.Append(new C.FirstSliceAngle { Val = 0 });
        doughnut.Append(new C.HoleSize { Val = 55 });
        return doughnut;
    }

    private static C.PieChartSeries BuildPieSeries(ChartPlacement placement, string quotedSheet, int seriesIndex)
    {
        var seriesColumn = ColumnLetter(seriesIndex + 2);
        var seriesText = BuildSeriesText(quotedSheet, seriesColumn);

        // Donut slices are categories, not series — colour each slice from
        // ReportColors.SeriesAt by category index, mirroring CustomChartSvg.RenderDonut's
        // per-category colouring (which also reads only the first series).
        var children = new List<OpenXmlElement>
        {
            new C.Index { Val = (uint)seriesIndex },
            new C.Order { Val = (uint)seriesIndex },
            seriesText
        };
        for (var c = 0; c < placement.CategoryCount; c++)
        {
            children.Add(new C.DataPoint(
                new C.Index { Val = (uint)c },
                new C.ChartShapeProperties(new A.SolidFill(
                    new A.RgbColorModelHex { Val = ToHex(ReportColors.SeriesAt(c)) }))));
        }

        children.Add(BuildCategoryAxisData(quotedSheet, placement.CategoryCount));
        children.Add(BuildValues(quotedSheet, seriesColumn, placement.CategoryCount));

        return new C.PieChartSeries(children);
    }

    private static C.CategoryAxisData BuildCategoryAxisData(string quotedSheet, int categoryCount) =>
        new(new C.StringReference { Formula = new C.Formula($"{quotedSheet}!$A$2:$A${1 + categoryCount}") });

    private static C.Values BuildValues(string quotedSheet, string column, int categoryCount) =>
        new(new C.NumberReference { Formula = new C.Formula($"{quotedSheet}!${column}$2:${column}${1 + categoryCount}") });

    private static C.SeriesText BuildSeriesText(string quotedSheet, string column) =>
        new(new C.StringReference { Formula = new C.Formula($"{quotedSheet}!${column}$1") });

    // ---- anchoring ----

    private static Xdr.TwoCellAnchor BuildAnchor(string chartRelationshipId, int seriesCount)
    {
        // Anchor to the right of the data, roughly at column Series.Count + 3, spanning a
        // fixed 10-column by 20-row box — enough to read a chart without dominating the sheet.
        var fromColumn = seriesCount + 3;
        var toColumn = fromColumn + 10;

        return new Xdr.TwoCellAnchor(
            new Xdr.FromMarker(
                new Xdr.ColumnId(fromColumn.ToString()),
                new Xdr.ColumnOffset("0"),
                new Xdr.RowId("1"),
                new Xdr.RowOffset("0")),
            new Xdr.ToMarker(
                new Xdr.ColumnId(toColumn.ToString()),
                new Xdr.ColumnOffset("0"),
                new Xdr.RowId("21"),
                new Xdr.RowOffset("0")),
            new Xdr.GraphicFrame(
                new Xdr.NonVisualGraphicFrameProperties(
                    new Xdr.NonVisualDrawingProperties { Id = 2, Name = "Chart 1" },
                    new Xdr.NonVisualGraphicFrameDrawingProperties()),
                new Xdr.Transform(new A.Offset { X = 0, Y = 0 }, new A.Extents { Cx = 0, Cy = 0 }),
                new A.Graphic(
                    new A.GraphicData(new C.ChartReference { Id = chartRelationshipId })
                    {
                        Uri = "http://schemas.openxmlformats.org/drawingml/2006/chart"
                    }))
            {
                Macro = ""
            },
            new Xdr.ClientData());
    }

    private static void AppendDrawingReference(WorksheetPart worksheetPart, DrawingsPart drawingsPart)
    {
        var drawingRelationshipId = worksheetPart.GetIdOfPart(drawingsPart);
        var drawingReference = new X.Drawing { Id = drawingRelationshipId };

        // CT_Worksheet requires <drawing> to precede legacyDrawing/drawingHF/picture/
        // oleObjects/controls/webPublishItems/tableParts/extLst — insert before whichever of
        // those ClosedXML already emitted (commonly an empty <tableParts count="0"/>), or
        // append if none are present. A plain Append here would place <drawing> after
        // <tableParts>, which Excel treats as a corrupt file.
        var successor = worksheetPart.Worksheet.ChildElements.FirstOrDefault(el =>
            el is X.LegacyDrawing or X.LegacyDrawingHeaderFooter or X.Picture or X.OleObjects
                or X.Controls or X.WebPublishItems or X.TableParts or X.WorksheetExtensionList);

        if (successor is not null)
            worksheetPart.Worksheet.InsertBefore(drawingReference, successor);
        else
            worksheetPart.Worksheet.Append(drawingReference);

        worksheetPart.Worksheet.Save();
    }

    // ---- small helpers ----

    /// <summary>Sheet names come from user-authored block titles and may contain spaces or
    /// apostrophes (only <c>\ / ? * [ ] :</c> are stripped by <c>UniqueSheetName</c>), so a
    /// formula reference must quote the sheet name and double any embedded apostrophe —
    /// e.g. a sheet named <c>Ana's hours</c> becomes <c>'Ana''s hours'</c>.</summary>
    private static string QuoteSheetName(string sheetName) => "'" + sheetName.Replace("'", "''") + "'";

    /// <summary>OpenXml's RgbColorModelHex expects hex digits without the leading '#' that
    /// ReportColors' constants use.</summary>
    private static string ToHex(string colorHtml) => colorHtml.TrimStart('#');

    /// <summary>1-based column index to Excel column letters (1 -> A, 2 -> B, 27 -> AA, ...).</summary>
    private static string ColumnLetter(int oneBasedIndex)
    {
        var letters = string.Empty;
        var n = oneBasedIndex;
        while (n > 0)
        {
            var remainder = (n - 1) % 26;
            letters = (char)('A' + remainder) + letters;
            n = (n - 1) / 26;
        }

        return letters;
    }
}
