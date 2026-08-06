using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Constants;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Projects;

public sealed class ProjectThresholdService : IProjectThresholdService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ProjectThresholdService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ProjectThresholdDto>> ListAsync(
        Guid projectId,
        ProjectThresholdMetricType? metricType = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanManageProjectAsync(projectId, cancellationToken);

        var query = _db.ProjectThresholds
            .AsNoTracking()
            .Where(t => t.ProjectId == projectId);

        if (metricType is ProjectThresholdMetricType filter)
            query = query.Where(t => t.MetricType == filter);

        var thresholds = await query
            .OrderBy(t => t.MetricType)
            .ThenBy(t => t.ThresholdPercentage)
            .ToListAsync(cancellationToken);

        return thresholds.Select(ToDto).ToList();
    }

    public async Task<ProjectThresholdDto> CreateAsync(
        Guid projectId,
        CreateProjectThresholdInput input,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanManageProjectAsync(projectId, cancellationToken);
        ValidatePercentage(input.ThresholdPercentage);
        ValidateMetricType(input.MetricType);

        var duplicate = await _db.ProjectThresholds
            .AnyAsync(
                t => t.ProjectId == projectId
                     && t.MetricType == input.MetricType
                     && t.ThresholdPercentage == input.ThresholdPercentage,
                cancellationToken);

        if (duplicate)
            throw new AppException("A threshold with this percentage already exists for the project metric.", 409);

        var now = DateTime.UtcNow;
        var entity = new ProjectThreshold
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            MetricType = input.MetricType,
            ThresholdPercentage = input.ThresholdPercentage,
            IsTriggered = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _db.ProjectThresholds.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task<ProjectThresholdDto> UpdateAsync(
        Guid projectId,
        Guid thresholdId,
        UpdateProjectThresholdInput input,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanManageProjectAsync(projectId, cancellationToken);
        ValidatePercentage(input.ThresholdPercentage);

        var entity = await _db.ProjectThresholds
            .FirstOrDefaultAsync(t => t.Id == thresholdId && t.ProjectId == projectId, cancellationToken)
            ?? throw new AppException("Project threshold not found.", 404);

        var duplicate = await _db.ProjectThresholds
            .AnyAsync(
                t => t.ProjectId == projectId
                     && t.Id != thresholdId
                     && t.MetricType == entity.MetricType
                     && t.ThresholdPercentage == input.ThresholdPercentage,
                cancellationToken);

        if (duplicate)
            throw new AppException("A threshold with this percentage already exists for the project metric.", 409);

        if (entity.ThresholdPercentage != input.ThresholdPercentage)
        {
            entity.ThresholdPercentage = input.ThresholdPercentage;
            entity.IsTriggered = false;
        }

        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        return ToDto(entity);
    }

    public async Task DeleteAsync(
        Guid projectId,
        Guid thresholdId,
        CancellationToken cancellationToken = default)
    {
        await EnsureCanManageProjectAsync(projectId, cancellationToken);

        var entity = await _db.ProjectThresholds
            .FirstOrDefaultAsync(t => t.Id == thresholdId && t.ProjectId == projectId, cancellationToken)
            ?? throw new AppException("Project threshold not found.", 404);

        var undelivered = await _db.PendingProjectAlerts
            .Where(a => a.ThresholdId == thresholdId && a.DeliveredAtUtc == null)
            .ToListAsync(cancellationToken);

        _db.PendingProjectAlerts.RemoveRange(undelivered);
        _db.ProjectThresholds.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Admins may manage thresholds on any project. Project Managers may only
    /// manage thresholds on projects they created.
    /// </summary>
    private async Task EnsureCanManageProjectAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await _db.Projects
            .AsNoTracking()
            .Where(p => p.Id == projectId)
            .Select(p => new { p.Id, p.CreatedByUserId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new AppException("Project not found.", 404);

        if (_currentUser.Roles.Contains(RoleNames.Admin, StringComparer.Ordinal))
            return;

        if (project.CreatedByUserId != _currentUser.UserId)
            throw AppErrors.Forbidden("You can only manage thresholds on projects you created.");
    }

    private static void ValidatePercentage(decimal percentage)
    {
        if (percentage <= 0m || percentage > 100m)
            throw new AppException("Threshold percentage must be greater than 0 and at most 100.", 400);
    }

    private static void ValidateMetricType(ProjectThresholdMetricType metricType)
    {
        if (!Enum.IsDefined(metricType))
            throw new AppException("Invalid threshold metric type.", 400);
    }

    private static ProjectThresholdDto ToDto(ProjectThreshold entity) =>
        new()
        {
            Id = entity.Id,
            ProjectId = entity.ProjectId,
            MetricType = entity.MetricType,
            ThresholdPercentage = entity.ThresholdPercentage,
            IsTriggered = entity.IsTriggered,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
}
