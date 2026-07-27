using ReeTrack.Application.Common.Models;

namespace ReeTrack.Infrastructure.Reports.Writers;

internal static class ReportFileNames
{
    public static string For(ReportExportFormat format, DateTime generatedAtUtc)
    {
        var date = generatedAtUtc.ToString("yyyyMMdd");
        var ext = format switch
        {
            ReportExportFormat.Csv => "csv",
            ReportExportFormat.Xlsx => "xlsx",
            ReportExportFormat.Pdf => "pdf",
            _ => "bin"
        };
        return $"reetrack-summary_{date}.{ext}";
    }
}
