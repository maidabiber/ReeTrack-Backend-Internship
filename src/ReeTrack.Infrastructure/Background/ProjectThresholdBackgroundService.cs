using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Options;
using ReeTrack.Infrastructure.Projects;

namespace ReeTrack.Infrastructure.Background;

public sealed class ProjectThresholdBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<ProjectThresholdOptions> _optionsMonitor;
    private readonly ILogger<ProjectThresholdBackgroundService> _logger;

    private DateOnly? _lastEvaluationDateUtc;
    private DateOnly? _lastDeliveryDateUtc;

    public ProjectThresholdBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<ProjectThresholdOptions> optionsMonitor,
        ILogger<ProjectThresholdBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _optionsMonitor = optionsMonitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Project Threshold Background Service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = _optionsMonitor.CurrentValue;
            var interval = TimeSpan.FromMinutes(Math.Max(1, options.PollIntervalMinutes));

            try
            {
                await ProcessPendingTasksAsync(options, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Project threshold background loop failed.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Project Threshold Background Service stopped.");
    }

    private async Task ProcessPendingTasksAsync(ProjectThresholdOptions options, CancellationToken stoppingToken)
    {
        var evaluationTime = ProjectThresholdEvaluationService.ParseTimeOfDay(
            options.EvaluationTimeUtc,
            new TimeOnly(2, 0));

        var deliveryTime = ProjectThresholdEvaluationService.ParseTimeOfDay(
            options.DeliveryTimeUtc,
            new TimeOnly(8, 0));

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var currentTime = TimeOnly.FromDateTime(now);

        if (currentTime >= evaluationTime && _lastEvaluationDateUtc != today)
        {
            using var scope = _scopeFactory.CreateScope();
            var evaluation = scope.ServiceProvider.GetRequiredService<IProjectThresholdEvaluationService>();

            var summary = await evaluation.EvaluateAsync(cancellationToken: stoppingToken);
            _lastEvaluationDateUtc = today;

            _logger.LogInformation(
                "Project threshold evaluation finished. Projects={Projects}, Triggered={Triggered}, Cleared={Cleared}, Pending={Pending}.",
                summary.ProjectsEvaluated,
                summary.ThresholdsTriggered,
                summary.ThresholdsCleared,
                summary.PendingCreated);
        }

        if (currentTime >= deliveryTime && _lastDeliveryDateUtc != today)
        {
            using var scope = _scopeFactory.CreateScope();
            var delivery = scope.ServiceProvider.GetRequiredService<IProjectThresholdDeliveryService>();

            var delivered = await delivery.DeliverPendingAsync(stoppingToken);
            _lastDeliveryDateUtc = today;

            _logger.LogInformation(
                "Project threshold delivery finished. NotificationsDelivered={Delivered}.",
                delivered);
        }
    }
}
