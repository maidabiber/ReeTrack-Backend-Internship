using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Domain.Services;

namespace ReeTrack.Infrastructure.Projects;

public sealed class ProjectCostService : IProjectCostService
{
    private readonly IApplicationDbContext _db;
    private readonly IProjectCostCalculator _calculator;

    public ProjectCostService(IApplicationDbContext db, IProjectCostCalculator calculator)
    {
        _db = db;
        _calculator = calculator;
    }

    public async Task<ProjectCostDto> CalculateAsync(
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        var project = await _db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, cancellationToken)
            ?? throw new AppException("Project not found.", 404);

        var entries = await _db.TimeEntries
            .AsNoTracking()
            .Where(e =>
                e.ProjectId == projectId &&
                e.Status == TimeEntryStatus.Confirmed &&
                e.DeletedAtUtc == null)
            .ToListAsync(cancellationToken);

        var userIds = entries
            .Select(e => e.UserId)
            .Distinct()
            .ToList();

        var userRates = userIds.Count == 0
            ? []
            : await _db.UserHourlyRates
                .AsNoTracking()
                .Where(r => userIds.Contains(r.UserId))
                .ToListAsync(cancellationToken);

        var calculatedCost = _calculator.Calculate(project, entries, userRates);
        var calculatedAtUtc = DateTime.UtcNow;

        var snapshot = new ProjectCostSnapshot
        {
            ProjectId = projectId,
            CalculatedCost = calculatedCost,
            CalculatedAtUtc = calculatedAtUtc,
            CreatedAtUtc = calculatedAtUtc,
            UpdatedAtUtc = calculatedAtUtc
        };

        _db.ProjectCostSnapshots.Add(snapshot);
        await _db.SaveChangesAsync(cancellationToken);

        return new ProjectCostDto
        {
            ProjectId = projectId,
            CalculatedCost = calculatedCost,
            CalculatedAtUtc = calculatedAtUtc
        };
    }
}
