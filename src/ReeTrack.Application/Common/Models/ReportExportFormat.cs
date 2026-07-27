namespace ReeTrack.Application.Common.Models;

public enum ReportExportFormat
{
    Csv,
    Xlsx,
    Pdf
}

public sealed record ReportFile(byte[] Bytes, string ContentType, string FileName);
