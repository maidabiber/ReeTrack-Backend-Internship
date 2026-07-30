using ReeTrack.Application.Common.Models;

namespace ReeTrack.Infrastructure.Reports.Writers;

internal static class ReportFileNames
{
    public static string For(ReportExportFormat format, DateTime generatedAtUtc) =>
        For("summary", format, generatedAtUtc);

    public static string ForDetailed(ReportExportFormat format, DateTime generatedAtUtc) =>
        For("detailed", format, generatedAtUtc);

    private static string For(string kind, ReportExportFormat format, DateTime generatedAtUtc)
    {
        var date = generatedAtUtc.ToString("yyyyMMdd");
        var ext = format switch
        {
            ReportExportFormat.Csv => "csv",
            ReportExportFormat.Xlsx => "xlsx",
            ReportExportFormat.Pdf => "pdf",
            _ => "bin"
        };
        return $"reetrack-{kind}_{date}.{ext}";
    }
}
