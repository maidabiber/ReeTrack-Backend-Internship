using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Options;
using ReeTrack.Infrastructure.HourTargets;

namespace ReeTrack.Infrastructure.Background;

public sealed class WeeklyTargetCheckInBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly WeeklyTargetCheckInOptions _options;
    private readonly ILogger<WeeklyTargetCheckInBackgroundService> _logger;

    public WeeklyTargetCheckInBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<WeeklyTargetCheckInOptions> options,
        ILogger<WeeklyTargetCheckInBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(15, _options.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);

                if (!IsInSendWindow(DateTime.UtcNow, _options))
                    continue;

                using var scope = _scopeFactory.CreateScope();
                var job = scope.ServiceProvider.GetRequiredService<IWeeklyTargetCheckInJob>();
                await job.RunAsync(DateTime.UtcNow, _options, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Weekly target check-in background loop failed.");
            }
        }
    }

    public static bool IsInSendWindow(DateTime utcNow, WeeklyTargetCheckInOptions options)
    {
        var timeZone = WeeklyTargetCheckInJob.ResolveTimeZone(options.TimeZone);
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), timeZone);

        if (local.DayOfWeek != options.DayOfWeek)
            return false;

        if (!TimeOnly.TryParse(options.AtLocalTime, out var at))
            at = new TimeOnly(12, 0);

        var localTime = TimeOnly.FromDateTime(local);
        // Fire during the configured minute (handles 60s polling).
        return localTime.Hour == at.Hour && localTime.Minute == at.Minute;
    }
}
