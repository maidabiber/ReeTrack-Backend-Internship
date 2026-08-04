using ReeTrack.Infrastructure.Reports.Custom;
using Xunit;

namespace ReeTrack.UnitTests.Reports.Custom;

public class DimensionCatalogTests
{
    [Fact]
    public void TagDimension_FansOut_FullHoursToEachTag()
    {
        var row = new EntryRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Ada",
            Guid.NewGuid(),
            "Alpha",
            Guid.NewGuid(),
            "Acme",
            null,
            "(No task)",
            [(Guid.NewGuid(), "Design"), (Guid.NewGuid(), "Build")],
            IsBillable: true,
            Date: new DateOnly(2026, 7, 1),
            WeekStart: new DateOnly(2026, 6, 29),
            CurrencyCode: "EUR",
            DurationSeconds: 3600,
            Description: null,
            Cost: null);

        var keys = DimensionCatalog.GetRequired("tag").KeysOf(row);
        Assert.Equal(2, keys.Count);
        Assert.Contains(keys, k => k.Label == "Design");
        Assert.Contains(keys, k => k.Label == "Build");
    }

    [Fact]
    public void TagDimension_Untagged_UsesNoTagsBucket()
    {
        var row = new EntryRow(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Ada",
            null,
            "Unassigned",
            null,
            "(No client)",
            null,
            "(No task)",
            [],
            IsBillable: false,
            Date: new DateOnly(2026, 7, 1),
            WeekStart: new DateOnly(2026, 6, 29),
            CurrencyCode: "—",
            DurationSeconds: 1800,
            Description: null,
            Cost: null);

        var key = Assert.Single(DimensionCatalog.GetRequired("tag").KeysOf(row));
        Assert.Equal("(No tags)", key.Label);
    }
}
