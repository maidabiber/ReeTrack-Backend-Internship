using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace ReeTrack.Infrastructure.Reports.Writers;

/// <summary>
/// Shared "Basis & assumptions" block for PDF exports. Originally only
/// <c>PdfReportWriter</c> (Summary) gave this a titled block with a heading and
/// bulleted lines — Detailed, Workload and Profitability each appended the same
/// kind of content as unheaded, ungrouped trailing text. Extracted so every
/// export looks like the same product and a future report type gets this for free.
/// </summary>
internal static class PdfBasisBlock
{
    public static void Compose(IContainer container, IReadOnlyList<string> lines)
    {
        if (lines.Count == 0) return;

        container.Column(col =>
        {
            col.Item().Text("Basis & assumptions").SemiBold().FontSize(11);
            col.Item().PaddingTop(4).PaddingBottom(8)
                .Height(1).Width(36).Background(ReportColors.Brand);

            foreach (var line in lines)
            {
                col.Item().PaddingBottom(3).Row(row =>
                {
                    row.ConstantItem(10).Text("•").FontSize(7.5f).FontColor(ReportColors.NavyMuted);
                    row.RelativeItem().Text(line)
                        .FontSize(7.5f).FontColor(ReportColors.NavyMuted).LineHeight(1.3f);
                });
            }
        });
    }
}
