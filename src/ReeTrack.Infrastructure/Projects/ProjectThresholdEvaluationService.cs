using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Options;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Projects;

public sealed class ProjectThresholdEvaluationService : IProjectThresholdEvaluationService
{
    private readonly IApplicationDbContext _db;
    private readonly IProjectCostService _projectCostService;
    private readonly ProjectThresholdOptions _options;
    private readonly ILogger<ProjectThresholdEvaluationService> _logger;

    public ProjectThresholdEvaluationService(
        IApplicationDbContext db,
        IProjectCostService projectCostService,
        IOptions<ProjectThresholdOptions> options,
        ILogger<ProjectThresholdEvaluationService> logger)
    {
        _db = db;
        _projectCostService = projectCostService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ProjectThresholdRunSummary> EvaluateAsync(
        Guid? projectId = null,
        bool deliverImmediately = false,
        CancellationToken cancellationToken = default)
    {
        var summary = new ProjectThresholdRunSummary();
        var now = DateTime.UtcNow;
        var deliverAfterUtc = deliverImmediately
            ? now
            : ResolveNextDeliveryUtc(now);

        var projectsQuery = _db.Projects
            .Include(p => p.Thresholds)
            .Where(p =>
                p.Status == ProjectStatus.Active &&
                p.Thresholds.Any());

        if (projectId is Guid filterId)
            projectsQuery = projectsQuery.Where(p => p.Id == filterId);

        var projects = await projectsQuery.ToListAsync(cancellationToken);
        if (projects.Count == 0)
            return summary;

        var actualHoursByProject = await ProjectActualHoursCalculator.GetActualHoursByProjectAsync(
            _db,
            projects.Select(p => p.Id).ToList(),
            cancellationToken);

        foreach (var project in projects)
        {
            summary.ProjectsEvaluated++;

            var costThresholds = project.Thresholds
                .Where(t => t.MetricType == ProjectThresholdMetricType.Cost)
                .ToList();
            var timeThresholds = project.Thresholds
                .Where(t => t.MetricType == ProjectThresholdMetricType.TimeEstimate)
                .ToList();

            if (costThresholds.Count > 0 && project.FixedFeeAmount is > 0m)
            {
                await EvaluateCostThresholdsAsync(
                    project,
                    costThresholds,
                    project.FixedFeeAmount.Value,
                    deliverAfterUtc,
                    now,
                    summary,
                    cancellationToken);
            }
            else
            {
                // Fixed fee missing: clear any previously triggered cost thresholds.
                foreach (var threshold in costThresholds.Where(t => t.IsTriggered))
                {
                    threshold.IsTriggered = false;
                    threshold.UpdatedAtUtc = now;
                    summary.ThresholdsCleared++;
                }
            }

            if (timeThresholds.Count > 0 && project.TimeEstimateHours is > 0m)
            {
                var actualHours = actualHoursByProject.GetValueOrDefault(project.Id);
                EvaluateTimeThresholds(
                    project,
                    timeThresholds,
                    actualHours,
                    project.TimeEstimateHours.Value,
                    deliverAfterUtc,
                    now,
                    summary);
            }
            else
            {
                foreach (var threshold in timeThresholds.Where(t => t.IsTriggered))
                {
                    threshold.IsTriggered = false;
                    threshold.UpdatedAtUtc = now;
                    summary.ThresholdsCleared++;
                }
            }
        }

        if (summary.PendingCreated > 0 || summary.ThresholdsCleared > 0)
            await _db.SaveChangesAsync(cancellationToken);

        return summary;
    }

    private async Task EvaluateCostThresholdsAsync(
        Project project,
        IReadOnlyList<ProjectThreshold> thresholds,
        decimal fixedFee,
        DateTime deliverAfterUtc,
        DateTime now,
        ProjectThresholdRunSummary summary,
        CancellationToken cancellationToken)
    {
        ProjectCostDto cost;
        try
        {
            cost = await _projectCostService.CalculateAsync(project.Id, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Skipping cost threshold evaluation for project {ProjectId}; cost calculation failed.",
                project.Id);
            return;
        }

        var costPercent = Math.Round(cost.CalculatedCost / fixedFee * 100m, 2, MidpointRounding.AwayFromZero);

        foreach (var threshold in thresholds)
        {
            if (costPercent >= threshold.ThresholdPercentage && !threshold.IsTriggered)
            {
                _db.PendingProjectAlerts.Add(new PendingProjectAlert
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    ThresholdId = threshold.Id,
                    MetricType = ProjectThresholdMetricType.Cost,
                    ProjectName = project.Name,
                    ThresholdPercentage = threshold.ThresholdPercentage,
                    CostPercentage = costPercent,
                    CalculatedCost = cost.CalculatedCost,
                    FixedFeeAmount = fixedFee,
                    CurrencyCode = project.CurrencyCode,
                    HoursPercentage = 0m,
                    ActualHours = 0m,
                    TimeEstimateHours = 0m,
                    DeliverAfterUtc = deliverAfterUtc,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });

                threshold.IsTriggered = true;
                threshold.UpdatedAtUtc = now;
                summary.ThresholdsTriggered++;
                summary.PendingCreated++;
            }
            else if (costPercent < threshold.ThresholdPercentage && threshold.IsTriggered)
            {
                threshold.IsTriggered = false;
                threshold.UpdatedAtUtc = now;
                summary.ThresholdsCleared++;
            }
        }
    }

    private void EvaluateTimeThresholds(
        Project project,
        IReadOnlyList<ProjectThreshold> thresholds,
        decimal actualHours,
        decimal timeEstimateHours,
        DateTime deliverAfterUtc,
        DateTime now,
        ProjectThresholdRunSummary summary)
    {
        var hoursPercent = Math.Round(actualHours / timeEstimateHours * 100m, 2, MidpointRounding.AwayFromZero);

        foreach (var threshold in thresholds)
        {
            if (hoursPercent >= threshold.ThresholdPercentage && !threshold.IsTriggered)
            {
                _db.PendingProjectAlerts.Add(new PendingProjectAlert
                {
                    Id = Guid.NewGuid(),
                    ProjectId = project.Id,
                    ThresholdId = threshold.Id,
                    MetricType = ProjectThresholdMetricType.TimeEstimate,
                    ProjectName = project.Name,
                    ThresholdPercentage = threshold.ThresholdPercentage,
                    CostPercentage = 0m,
                    CalculatedCost = 0m,
                    FixedFeeAmount = 0m,
                    CurrencyCode = project.CurrencyCode,
                    HoursPercentage = hoursPercent,
                    ActualHours = actualHours,
                    TimeEstimateHours = timeEstimateHours,
                    DeliverAfterUtc = deliverAfterUtc,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });

                threshold.IsTriggered = true;
                threshold.UpdatedAtUtc = now;
                summary.ThresholdsTriggered++;
                summary.PendingCreated++;
            }
            else if (hoursPercent < threshold.ThresholdPercentage && threshold.IsTriggered)
            {
                threshold.IsTriggered = false;
                threshold.UpdatedAtUtc = now;
                summary.ThresholdsCleared++;
            }
        }
    }

    private DateTime ResolveNextDeliveryUtc(DateTime utcNow)
    {
        var deliveryTime = ParseTimeOfDay(_options.DeliveryTimeUtc, new TimeOnly(8, 0));
        var candidate = new DateTime(
            utcNow.Year,
            utcNow.Month,
            utcNow.Day,
            deliveryTime.Hour,
            deliveryTime.Minute,
            0,
            DateTimeKind.Utc);

        if (candidate <= utcNow)
            candidate = candidate.AddDays(1);

        return candidate;
    }

    internal static TimeOnly ParseTimeOfDay(string? value, TimeOnly fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        return TimeOnly.TryParse(value, out var parsed) ? parsed : fallback;
    }
}
