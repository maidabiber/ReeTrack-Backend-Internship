namespace ReeTrack.Infrastructure.Reports.Writers;

/// <summary>
/// Brand colours mirrored from frontend design tokens (src/index.css).
/// Hardcoded hex — the backend cannot import the CSS.
/// Palette is blue + purple only (no green / orange / yellow).
/// </summary>
public static class ReportColors
{
    // Brand — blue ↔ purple
    public const string Brand = "#4366E2";
    public const string BrandDeep = "#3552C4";
    public const string BrandTint = "#EEF1FD";
    public const string BrandHi = "#BF6DE6";
    public const string PurpleMid = "#9B7AE8";
    public const string PurpleDeep = "#8B5CF6";
    public const string Blue = "#2563EB";
    public const string PurpleVeil = "#F6EEFB";

    // Ink
    public const string Navy = "#1B2540";
    public const string NavyMuted = "#5A647A";

    // Surfaces / neutrals
    public const string SurfaceMuted = "#F2F4F9";
    public const string Canvas = "#F7F8FB";
    public const string White = "#FFFFFF";
    public const string Gray = "#4B5563";
    public const string HeaderGray = "#6B7280";
    public const string HeaderGrayBg = "#E5E7EB";

    // Semantic
    public const string Billable = Brand;
    public const string NonBillable = Gray;

    /// <summary>Blue/purple categorical series for bars (one meaning per index).</summary>
    public static readonly string[] Series =
    [
        Brand,
        BrandHi,
        Blue,
        PurpleMid,
        BrandDeep,
        PurpleDeep,
        Brand,
        BrandHi
    ];

    public static string SeriesAt(int index) =>
        Series[Math.Abs(index) % Series.Length];
}
