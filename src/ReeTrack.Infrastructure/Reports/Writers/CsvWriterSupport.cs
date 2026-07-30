using System.Globalization;
using System.Text;

namespace ReeTrack.Infrastructure.Reports.Writers;

/// <summary>
/// Shared plumbing for the CSV writers: field escaping, decimal formatting, the UTF-8 BOM
/// prefix, and the "Overview,Key,Value" row shape used by three of the four writers.
/// </summary>
internal static class CsvWriterSupport
{
    /// <summary>
    /// RFC-4180 field escaping: quote when needed; double internal quotes. Also
    /// neutralises spreadsheet formula injection — Excel / Sheets execute a cell that
    /// opens with = + - @ or a leading tab/CR, and several of these columns are
    /// user-supplied (names, descriptions). Previously only the Summary writer did this;
    /// the other three writers quoted commas/quotes but left formula triggers unescaped.
    /// </summary>
    public static string Escape(string? value)
    {
        value ??= string.Empty;
        if (IsFormulaTrigger(value))
            value = "'" + value;

        if (value.Contains('"') || value.Contains(',') || value.Contains('\r') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }

    private static bool IsFormulaTrigger(string value) =>
        value.Length > 0 && value[0] is '=' or '+' or '-' or '@' or '\t' or '\r';

    /// <summary>
    /// Bounded, trimmed precision for a CSV number cell. Was "0.####" in two writers,
    /// "0.##" in a third, and an unbounded default <c>ToString()</c> in the fourth — every
    /// value passed through this already carries at most 4 decimal places by the time it
    /// reaches a writer (see <see cref="ReportRounding"/> / <c>SummaryReportAnalytics.Hours</c>),
    /// so standardising on "0.####" changes no displayed digit, just removes the drift.
    /// </summary>
    public static string FormatDecimal(decimal value) =>
        value.ToString("0.####", CultureInfo.InvariantCulture);

    /// <summary>UTF-8 with BOM, so Excel opens the file as UTF-8 instead of guessing the
    /// system codepage and mangling non-ASCII names.</summary>
    public static byte[] ToUtf8BytesWithBom(StringBuilder sb)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var bytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);
        return bytes;
    }

    /// <summary>
    /// The "Overview,Key,Value" row shape shared by Detailed/Workload/Profitability.
    /// Summary uses a differently-labelled "Summary,Key,Value" section, so it keeps its
    /// own <c>AppendKpi</c> rather than taking a section-name parameter here.
    /// </summary>
    public static void AppendOverview(StringBuilder sb, string key, object value) =>
        sb.Append("Overview,").Append(Escape(key)).Append(',').AppendLine(Escape(value?.ToString() ?? ""));
}
