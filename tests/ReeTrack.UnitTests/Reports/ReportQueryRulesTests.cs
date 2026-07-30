using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Reports;
using Xunit;

namespace ReeTrack.UnitTests.Reports;

public class ReportQueryRulesTests
{
    [Fact]
    public void NormalizeAndValidate_DeduplicatesIdsAndGroups()
    {
        var id = Guid.NewGuid();
        var query = new ReportQuery
        {
            UserIds = [Guid.Empty, id, id],
            GroupBy = [ReportGroupBy.User, ReportGroupBy.User]
        };

        var normalized = ReportQueryRules.NormalizeAndValidate(query);

        Assert.Equal([id], normalized.UserIds);
        Assert.Equal([ReportGroupBy.User], normalized.GroupBy);
    }

    [Fact]
    public void NormalizeAndValidate_StartAfterEnd_ThrowsBadRequest()
    {
        var query = new ReportQuery
        {
            From = new DateOnly(2026, 7, 2),
            To = new DateOnly(2026, 7, 1)
        };

        var error = Assert.Throws<AppException>(
            () => ReportQueryRules.NormalizeAndValidate(query));

        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public void NormalizeAndValidate_ExplicitRangeOver400Days_ThrowsBadRequest()
    {
        var query = new ReportQuery
        {
            From = new DateOnly(2024, 1, 1),
            To = new DateOnly(2026, 1, 1) // ~731 days
        };

        var error = Assert.Throws<AppException>(
            () => ReportQueryRules.NormalizeAndValidate(query));

        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public void NormalizeAndValidate_ExplicitRangeExactlyAtLimit_Passes()
    {
        var query = new ReportQuery
        {
            From = new DateOnly(2025, 1, 1),
            To = new DateOnly(2025, 1, 1).AddDays(400)
        };

        var normalized = ReportQueryRules.NormalizeAndValidate(query);

        Assert.Equal(query.From, normalized.From);
        Assert.Equal(query.To, normalized.To);
    }

    [Fact]
    public void NormalizeAndValidate_FullyOpenRange_IsNotBoundByTheRangeGuard()
    {
        // "All time" (no From, no To) is an intentionally supported report — the
        // max-range guard only fires on an explicit From+To span, not this case.
        var query = new ReportQuery();

        var normalized = ReportQueryRules.NormalizeAndValidate(query);

        Assert.Null(normalized.From);
        Assert.Null(normalized.To);
    }

    [Fact]
    public void NormalizeAndValidate_TooManyValues_ThrowsBadRequest()
    {
        var query = new ReportQuery
        {
            ProjectIds = Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToList()
        };

        var error = Assert.Throws<AppException>(
            () => ReportQueryRules.NormalizeAndValidate(query));

        Assert.Equal(400, error.StatusCode);
    }
}
