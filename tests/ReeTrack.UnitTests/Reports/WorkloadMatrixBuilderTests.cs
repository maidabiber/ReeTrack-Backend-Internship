using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Reports;
using Xunit;

namespace ReeTrack.UnitTests.Reports;

public class WorkloadMatrixBuilderTests
{
    private static readonly Guid AdaId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BenId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ClientId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ProjectAlphaId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ProjectBetaId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [Fact]
    public void Build_EmptyEntries_ReturnsEmptyWithZeroTotals()
    {
        var (allocations, grandTotal, grandBillable) = WorkloadMatrixBuilder.Build([]);

        Assert.Empty(allocations);
        Assert.Equal(0, grandTotal);
        Assert.Equal(0, grandBillable);
    }

    [Fact]
    public void Build_GroupsByMemberClientProject_AndComputesPctOfMemberTotal()
    {
        var user = User("Ada", AdaId);
        var client = new Client { Id = ClientId, Name = "Acme" };
        var project = new Project { Id = ProjectAlphaId, Name = "Alpha", ClientId = ClientId, Client = client };

        var entries = new List<TimeEntry>
        {
            Entry(AdaId, user, projectId: ProjectAlphaId, project: project, seconds: 3600, billable: true),
            Entry(AdaId, user, projectId: ProjectAlphaId, project: project, seconds: 1800, billable: false)
        };

        var (allocations, grandTotal, grandBillable) = WorkloadMatrixBuilder.Build(entries);

        var row = Assert.Single(allocations);
        Assert.Equal("Ada", row.DisplayName);
        Assert.Equal(ClientId, row.ClientId);
        Assert.Equal("Acme", row.ClientName);
        Assert.Equal(ProjectAlphaId, row.ProjectId);
        Assert.Equal("Alpha", row.ProjectName);
        Assert.Equal(5400, row.TotalSeconds);
        Assert.Equal(3600, row.BillableSeconds);
        Assert.Equal(100m, row.PctOfMemberTotal); // Ada's only allocation -> 100% of her own total
        Assert.Equal(5400, grandTotal);
        Assert.Equal(3600, grandBillable);
    }

    [Fact]
    public void Build_SplitsOneMemberAcrossTwoProjects_PctOfMemberTotalSumsTo100()
    {
        var user = User("Ada", AdaId);
        var client = new Client { Id = ClientId, Name = "Acme" };
        var alpha = new Project { Id = ProjectAlphaId, Name = "Alpha", ClientId = ClientId, Client = client };
        var beta = new Project { Id = ProjectBetaId, Name = "Beta", ClientId = ClientId, Client = client };

        var entries = new List<TimeEntry>
        {
            Entry(AdaId, user, ProjectAlphaId, alpha, seconds: 3600, billable: true),
            Entry(AdaId, user, ProjectBetaId, beta, seconds: 7200, billable: true)
        };

        var (allocations, _, _) = WorkloadMatrixBuilder.Build(entries);

        Assert.Equal(2, allocations.Count);
        var alphaRow = Assert.Single(allocations, a => a.ProjectName == "Alpha");
        var betaRow = Assert.Single(allocations, a => a.ProjectName == "Beta");
        Assert.Equal(33.33m, alphaRow.PctOfMemberTotal);
        Assert.Equal(66.67m, betaRow.PctOfMemberTotal);
    }

    [Fact]
    public void Build_OrdersByMemberTotalDescending_ThenDisplayName()
    {
        var ada = User("Ada", AdaId);
        var ben = User("Ben", BenId);
        var client = new Client { Id = ClientId, Name = "Acme" };
        var project = new Project { Id = ProjectAlphaId, Name = "Alpha", ClientId = ClientId, Client = client };

        var entries = new List<TimeEntry>
        {
            // Ben logs less time than Ada, so Ada's row should come first.
            Entry(BenId, ben, ProjectAlphaId, project, seconds: 1800, billable: true),
            Entry(AdaId, ada, ProjectAlphaId, project, seconds: 3600, billable: true)
        };

        var (allocations, _, _) = WorkloadMatrixBuilder.Build(entries);

        Assert.Equal("Ada", allocations[0].DisplayName);
        Assert.Equal("Ben", allocations[1].DisplayName);
    }

    [Fact]
    public void Build_UnassignedProject_UsesUnassignedLabel_AndNoClientUsesFallback()
    {
        var user = User("Ada", AdaId);
        var entries = new List<TimeEntry>
        {
            Entry(AdaId, user, projectId: null, project: null, seconds: 3600, billable: true)
        };

        var (allocations, _, _) = WorkloadMatrixBuilder.Build(entries);

        var row = Assert.Single(allocations);
        Assert.Equal("(Unassigned)", row.ProjectName);
        Assert.Equal("(No client)", row.ClientName);
        Assert.Null(row.ClientId);
    }

    private static User User(string displayName, Guid id) =>
        new()
        {
            Id = id,
            Email = $"{displayName.ToLowerInvariant()}@reetrack.test",
            DisplayName = displayName,
            Status = UserStatus.Active,
            CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };

    private static TimeEntry Entry(
        Guid userId,
        User user,
        Guid? projectId,
        Project? project,
        int seconds,
        bool billable) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            User = user,
            ProjectId = projectId,
            Project = project,
            IsBillable = billable,
            DurationSeconds = seconds,
            StartedAtUtc = new DateTime(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc),
            Status = TimeEntryStatus.Confirmed,
            CreatedAtUtc = new DateTime(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 7, 20, 9, 0, 0, DateTimeKind.Utc)
        };
}
