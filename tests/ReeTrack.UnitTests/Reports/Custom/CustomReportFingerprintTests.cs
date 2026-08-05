using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Models.CustomReports;
using ReeTrack.Infrastructure.Reports.Custom;
using Xunit;

namespace ReeTrack.UnitTests.Reports.Custom;

/// <summary>
/// <see cref="CustomReportFingerprint"/> exposes two hashes over the same spec that must stay
/// distinct: <c>Compute</c> strips narrative commentary so storing generated text can't
/// invalidate the fingerprint it was stored against, while <c>ComputeCacheKey</c> hashes the
/// spec unstripped because <see cref="BlockEvaluators"/> renders narrative blocks straight from
/// their cached text — a cache keyed on the stripped hash would serve one report's export with
/// another report's commentary baked in.
/// </summary>
public class CustomReportFingerprintTests
{
    private static CustomReportSpec Spec(
        DateOnly? from = null,
        ComparisonMode comparison = ComparisonMode.None,
        NarrativeBlockSpec? narrative = null) =>
        new()
        {
            Query = new ReportQuery { From = from ?? new DateOnly(2026, 7, 1), To = new DateOnly(2026, 7, 31) },
            Comparison = comparison,
            Blocks =
            [
                new KpiBlockSpec { Id = "k1", Metrics = ["totalHours"] },
                narrative ?? new NarrativeBlockSpec { Id = "n1" }
            ]
        };

    [Fact]
    public void Compute_CollidesForSpecsThatDifferOnlyInNarrativeText_ButComputeCacheKey_DoesNot()
    {
        // §5b: two specs whose only difference is stored narrative commentary. Compute must
        // treat them as the same report shape (that's the point of stripping); ComputeCacheKey
        // must treat them as different runs, or an export could serve one report's cached
        // commentary under another report's identity.
        var withoutNarrative = Spec();
        var withNarrative = Spec(narrative: new NarrativeBlockSpec
        {
            Id = "n1",
            CachedText = "Billable hours grew 12% quarter over quarter.",
            GeneratedAtUtc = DateTime.UtcNow
        });

        Assert.Equal(
            CustomReportFingerprint.Compute(withoutNarrative),
            CustomReportFingerprint.Compute(withNarrative));

        Assert.NotEqual(
            CustomReportFingerprint.ComputeCacheKey(withoutNarrative),
            CustomReportFingerprint.ComputeCacheKey(withNarrative));
    }

    [Fact]
    public void Compute_IsStableForTheSameSpec()
    {
        Assert.Equal(CustomReportFingerprint.Compute(Spec()), CustomReportFingerprint.Compute(Spec()));
    }

    [Fact]
    public void Compute_ChangesWithTheDateRange()
    {
        Assert.NotEqual(
            CustomReportFingerprint.Compute(Spec()),
            CustomReportFingerprint.Compute(Spec(from: new DateOnly(2026, 6, 1))));
    }

    [Fact]
    public void Compute_ChangesWithTheComparisonMode()
    {
        // Commentary written without a baseline reads differently from commentary with one.
        Assert.NotEqual(
            CustomReportFingerprint.Compute(Spec()),
            CustomReportFingerprint.Compute(Spec(comparison: ComparisonMode.PreviousPeriod)));
    }

    [Fact]
    public void Compute_IgnoresTheGeneratedTextItself()
    {
        // Storing a summary must not invalidate the fingerprint it was stored against.
        var before = CustomReportFingerprint.Compute(Spec());
        var after = CustomReportFingerprint.Compute(Spec(narrative: new NarrativeBlockSpec
        {
            Id = "n1",
            CachedText = "Acme grew.",
            GeneratedAtUtc = DateTime.UtcNow,
            GeneratedForFingerprint = before
        }));

        Assert.Equal(before, after);
    }

    [Fact]
    public void Compute_ChangesWithTheNarrativeFocus()
    {
        // Focus steers what the model writes about, so old text no longer answers the question.
        Assert.NotEqual(
            CustomReportFingerprint.Compute(Spec()),
            CustomReportFingerprint.Compute(Spec(narrative: new NarrativeBlockSpec
            {
                Id = "n1",
                Focus = "margin by client"
            })));
    }

    [Fact]
    public void ComputeCacheKey_IsStableForTheSameSpec()
    {
        Assert.Equal(
            CustomReportFingerprint.ComputeCacheKey(Spec()),
            CustomReportFingerprint.ComputeCacheKey(Spec()));
    }
}
