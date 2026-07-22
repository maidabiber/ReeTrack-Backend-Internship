using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Persistence;
using ReeTrack.Infrastructure.TimeEntries;
using ReeTrack.Infrastructure.Timesheets;
using Xunit;

namespace ReeTrack.UnitTests.TimeEntries;

public class TimeEntryServiceAssociationTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly Guid _userId;
    private readonly TimeEntryService _service;
    private readonly Guid _clientId;
    private readonly Guid _projectId;
    private readonly Guid _taskId;
    private readonly Guid _otherProjectId;
    private readonly Guid _tagId;

    public TimeEntryServiceAssociationTests()
    {
        _userId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TimeEntryAssociationTests_{Guid.NewGuid()}")
            .Options;

        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        SeedUser();

        _clientId = Guid.NewGuid();
        _projectId = Guid.NewGuid();
        _taskId = Guid.NewGuid();
        _otherProjectId = Guid.NewGuid();
        _tagId = Guid.NewGuid();
        SeedProjectGraph();

        _service = TimeEntryServiceTestDependencies.CreateTimeEntryService(
            _db,
            new FakeCurrentUser(_userId),
            new TimeEntryGuardService(_db, new PermissiveLockedPeriodService()));
    }

    [Fact]
    public async Task CreateManualEntry_WithProjectTaskAndTags_PersistsAssociations()
    {
        var startedAtUtc = DateTime.UtcNow.AddHours(-2);
        var endedAtUtc = DateTime.UtcNow.AddHours(-1);

        var result = await _service.CreateManualEntryAsync(new CreateManualEntryInput
        {
            Description = "Billable project work",
            StartedAtUtc = startedAtUtc,
            EndedAtUtc = endedAtUtc,
            ProjectId = _projectId,
            ProjectTaskId = _taskId,
            TagIds = [_tagId]
        });

        Assert.Equal(_projectId, result.Entry.ProjectId);
        Assert.Equal("Redesign", result.Entry.ProjectName);
        Assert.Equal("#4366E2", result.Entry.ProjectColor);
        Assert.Equal(_taskId, result.Entry.ProjectTaskId);
        Assert.Equal("Wireframes", result.Entry.ProjectTaskName);
        Assert.Single(result.Entry.Tags);
        Assert.Equal(_tagId, result.Entry.Tags[0].Id);

        var stored = await _db.TimeEntries
            .Include(e => e.TimeEntryTags)
            .SingleAsync();
        Assert.Equal(_projectId, stored.ProjectId);
        Assert.Equal(_taskId, stored.ProjectTaskId);
        Assert.Equal(_clientId, stored.ClientId);
        Assert.Single(stored.TimeEntryTags);
    }

    [Fact]
    public async Task CreateManualEntry_TaskOnly_InfersProject()
    {
        var startedAtUtc = DateTime.UtcNow.AddHours(-2);
        var endedAtUtc = DateTime.UtcNow.AddHours(-1);

        var result = await _service.CreateManualEntryAsync(new CreateManualEntryInput
        {
            StartedAtUtc = startedAtUtc,
            EndedAtUtc = endedAtUtc,
            ProjectTaskId = _taskId
        });

        Assert.Equal(_projectId, result.Entry.ProjectId);
        Assert.Equal(_taskId, result.Entry.ProjectTaskId);
    }

    [Fact]
    public async Task CreateManualEntry_TaskProjectMismatch_Throws()
    {
        var startedAtUtc = DateTime.UtcNow.AddHours(-2);
        var endedAtUtc = DateTime.UtcNow.AddHours(-1);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _service.CreateManualEntryAsync(new CreateManualEntryInput
            {
                StartedAtUtc = startedAtUtc,
                EndedAtUtc = endedAtUtc,
                ProjectId = _otherProjectId,
                ProjectTaskId = _taskId
            }));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task StartTimer_WithProject_PersistsProject()
    {
        var entry = await _service.StartTimerAsync(new StartTimerInput
        {
            Description = "Focus",
            ProjectId = _projectId,
            TagIds = [_tagId]
        });

        Assert.Equal(_projectId, entry.ProjectId);
        Assert.Equal("Redesign", entry.ProjectName);
        Assert.Single(entry.Tags);
    }

    [Fact]
    public async Task StopTimer_WithProjectAndTags_PersistsAssociations()
    {
        await _service.StartTimerAsync(new StartTimerInput
        {
            Description = "Focus"
        });

        var stopped = await _service.StopTimerAsync(new StopTimerInput
        {
            ProjectId = _projectId,
            ProjectTaskId = _taskId,
            TagIds = [_tagId],
            IsBillable = false
        });

        Assert.Equal(_projectId, stopped.ProjectId);
        Assert.Equal(_taskId, stopped.ProjectTaskId);
        Assert.Single(stopped.Tags);
        Assert.Equal(_tagId, stopped.Tags[0].Id);
        Assert.False(stopped.IsBillable);
    }

    [Fact]
    public async Task StopTimer_OmittingAssociations_PreservesExisting()
    {
        await _service.StartTimerAsync(new StartTimerInput
        {
            Description = "Focus",
            ProjectId = _projectId,
            ProjectTaskId = _taskId,
            TagIds = [_tagId]
        });

        var stopped = await _service.StopTimerAsync(new StopTimerInput
        {
            Description = "Focus (done)"
        });

        Assert.Equal(_projectId, stopped.ProjectId);
        Assert.Equal(_taskId, stopped.ProjectTaskId);
        Assert.Single(stopped.Tags);
    }

    [Fact]
    public async Task UpdateTimeEntry_OmittingAssociations_PreservesExisting()
    {
        var startedAtUtc = DateTime.UtcNow.AddHours(-3);
        var endedAtUtc = DateTime.UtcNow.AddHours(-2);
        var created = await _service.CreateManualEntryAsync(new CreateManualEntryInput
        {
            Description = "Keep tags",
            StartedAtUtc = startedAtUtc,
            EndedAtUtc = endedAtUtc,
            ProjectId = _projectId,
            ProjectTaskId = _taskId,
            TagIds = [_tagId]
        });

        var updated = await _service.UpdateTimeEntryAsync(created.Entry.Id, new UpdateTimeEntryInput
        {
            Description = "Updated desc",
            StartedAtUtc = startedAtUtc,
            EndedAtUtc = endedAtUtc,
            IsBillable = true
        });

        Assert.Equal(_projectId, updated.Entry.ProjectId);
        Assert.Equal(_taskId, updated.Entry.ProjectTaskId);
        Assert.Single(updated.Entry.Tags);
    }

    public void Dispose() => _db.Dispose();

    private void SeedUser()
    {
        var now = DateTime.UtcNow;
        _db.Users.Add(new User
        {
            Id = _userId,
            Email = "assoc.test@reetrack.test",
            Status = UserStatus.Active,
            EmailVerified = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        _db.SaveChanges();
    }

    private void SeedProjectGraph()
    {
        var now = DateTime.UtcNow;
        _db.Clients.Add(new Client
        {
            Id = _clientId,
            Name = "Acme",
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        _db.Projects.Add(new Project
        {
            Id = _projectId,
            ClientId = _clientId,
            Name = "Redesign",
            Status = ProjectStatus.Active,
            Color = "#4366E2",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        _db.Projects.Add(new Project
        {
            Id = _otherProjectId,
            ClientId = _clientId,
            Name = "Other",
            Status = ProjectStatus.Active,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        _db.ProjectTasks.Add(new ProjectTask
        {
            Id = _taskId,
            ProjectId = _projectId,
            Name = "Wireframes",
            Status = ProjectTaskStatus.Open,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        _db.Tags.Add(new Tag
        {
            Id = _tagId,
            Name = "Design",
            Color = "#22C55E",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        _db.SaveChanges();
    }

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUserService
    {
        public Guid UserId { get; } = userId;
        public IReadOnlyList<string> Roles { get; } = [];
        public bool IsAuthenticated => true;
    }

    private sealed class PermissiveLockedPeriodService : ILockedPeriodService
    {
        public Task EnsureEntryEditableAsync(DateTime startedAtUtc, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
