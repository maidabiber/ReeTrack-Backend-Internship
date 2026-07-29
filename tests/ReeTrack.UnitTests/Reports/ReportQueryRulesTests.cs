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
