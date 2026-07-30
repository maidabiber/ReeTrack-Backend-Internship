using System.Runtime.CompilerServices;
using QuestPDF.Infrastructure;

namespace ReeTrack.Infrastructure.Reports.Writers;

/// <summary>
/// Sets the QuestPDF Community license once for this assembly. Previously each of the 4
/// PDF writers repeated the same <c>static SomeWriter() { QuestPDF.Settings.License = ... }</c>
/// constructor; a module initializer runs once, before any type in this assembly is used
/// (including from tests, which construct writers directly without going through
/// <c>Program.cs</c>), so none of the writers need their own copy.
/// </summary>
internal static class PdfWriterLicense
{
    [ModuleInitializer]
    public static void SetLicense() => QuestPDF.Settings.License = LicenseType.Community;
}
