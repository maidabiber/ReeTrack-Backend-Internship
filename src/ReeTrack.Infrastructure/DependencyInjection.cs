using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReeTrack.Application.Calendar;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Options;
using ReeTrack.Application.Notifications;
using ReeTrack.Infrastructure.Auditing;
using ReeTrack.Application.Integrations.Calendar;
using ReeTrack.Infrastructure.Auth;
using ReeTrack.Infrastructure.Clients;
using ReeTrack.Infrastructure.Currencies;
using ReeTrack.Infrastructure.Invitations;
using ReeTrack.Infrastructure.Members;
using ReeTrack.Infrastructure.Notifications;
using ReeTrack.Infrastructure.Projects;
using ReeTrack.Infrastructure.RateMultipliers;
using ReeTrack.Infrastructure.HourTargets;
using ReeTrack.Infrastructure.Reports;
using ReeTrack.Infrastructure.Reports.Writers;
using ReeTrack.Infrastructure.Holidays;
using ReeTrack.Infrastructure.Tags;
using ReeTrack.Infrastructure.Teammates;
using ReeTrack.Infrastructure.TimeEntries;
using ReeTrack.Infrastructure.Timesheets;
using ReeTrack.Infrastructure.SmartTimeParse;
using ReeTrack.Infrastructure.UserHourlyRates;
using ReeTrack.Infrastructure.Background;
using ReeTrack.Infrastructure.Calendar;
using ReeTrack.Infrastructure.Integrations.Calendar;
using ReeTrack.Infrastructure.Integrations.Calendar.Google;
using ReeTrack.Infrastructure.Integrations.Jira;
using ReeTrack.Application.Integrations.Jira;
using ReeTrack.Domain.Services;

namespace ReeTrack.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GoogleAuthOptions>(configuration.GetSection(GoogleAuthOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<InvitationOptions>(configuration.GetSection(InvitationOptions.SectionName));
        services.Configure<AppOptions>(configuration.GetSection(AppOptions.SectionName));
        services.Configure<TimeEntryOptions>(configuration.GetSection(TimeEntryOptions.SectionName));
        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));
        services.Configure<JiraOptions>(configuration.GetSection(JiraOptions.SectionName));

        services.AddHttpContextAccessor();
        services.Configure<CalendarSyncOptions>(configuration.GetSection(CalendarSyncOptions.SectionName));

        services.AddHttpClient<IGoogleCodeExchanger, GoogleCodeExchanger>();
        services.AddHttpClient<GoogleCalendarProvider>();
        services.AddHttpClient<IJiraApiClient, JiraApiClient>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddHttpClient<INagerDateClient, NagerDateClient>(client =>
        {
            client.BaseAddress = new Uri("https://date.nager.at/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddScoped<IGoogleOAuthService, GoogleOAuthService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISetupService, SetupService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        services.AddScoped<ITransactionalEmailSender, TransactionalEmailSender>();
        services.AddScoped<IDomainEventPublisher, DomainEventPublisher>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<INotificationPreferenceService, NotificationPreferenceService>();
        services.AddScoped<IChannelProvider, EmailChannelProvider>();
        services.AddDomainEventHandlers(typeof(IDomainEventHandler<>).Assembly);

        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<IMemberService, MemberService>();
        services.AddScoped<IUserHourlyRateService, UserHourlyRateService>();
        services.AddScoped<ITeammateService, TeammateService>();
        services.AddScoped<ILockedPeriodService, LockedPeriodService>();
        services.AddScoped<ITimeEntryGuardService, TimeEntryGuardService>();
        services.AddScoped<ITimesheetService, TimesheetService>();
        services.AddScoped<ITimesheetReviewService, TimesheetReviewService>();
        services.AddScoped<ITimeEntryAssociationService, TimeEntryAssociationService>();
        services.AddScoped<ITimeEntryService, TimeEntryService>();
        services.AddScoped<ISharedTimeEntryService, SharedTimeEntryService>();
        services.AddScoped<ISharedTimeEntryApprovalService, SharedTimeEntryApprovalService>();
        services.AddScoped<ITimeEntryTemplateService, TimeEntryTemplateService>();
        services.AddScoped<ISmartTimeParseService, SmartTimeParseService>();
        services.AddScoped<ITimeEntrySuggestionService, TimeEntrySuggestionService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IProjectCostService, ProjectCostService>();
        services.AddScoped<ReportEntryPipeline>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IReportFilterSetService, ReportFilterSetService>();
        services.AddScoped<IReportWriter<SummaryReportDto>, CsvReportWriter>();
        services.AddScoped<IReportWriter<SummaryReportDto>, ExcelReportWriter>();
        services.AddScoped<IReportWriter<SummaryReportDto>, PdfReportWriter>();
        services.AddScoped<IReportWriter<DetailedReportDto>, CsvDetailedReportWriter>();
        services.AddScoped<IReportWriter<DetailedReportDto>, ExcelDetailedReportWriter>();
        services.AddScoped<IReportWriter<DetailedReportDto>, PdfDetailedReportWriter>();
        services.AddScoped<IReportWriter<WorkloadReportDto>, CsvWorkloadReportWriter>();
        services.AddScoped<IReportWriter<WorkloadReportDto>, ExcelWorkloadReportWriter>();
        services.AddScoped<IReportWriter<WorkloadReportDto>, PdfWorkloadReportWriter>();
        services.AddScoped<IReportWriter<ProfitabilityReportDto>, CsvProfitabilityReportWriter>();
        services.AddScoped<IReportWriter<ProfitabilityReportDto>, ExcelProfitabilityReportWriter>();
        services.AddScoped<IReportWriter<ProfitabilityReportDto>, PdfProfitabilityReportWriter>();
        services.AddScoped<IReportExportService, ReportExportService>();
        services.AddScoped<IRateMultiplierSettingsService, RateMultiplierSettingsService>();
        services.AddScoped<IRateMultiplierConfigProvider, RateMultiplierConfigProvider>();
        services.AddScoped<IHourTargetSettingsService, HourTargetSettingsService>();
        services.AddScoped<IUserHourTargetService, UserHourTargetService>();
        services.AddScoped<IHolidayService, HolidayService>();
        services.AddScoped<IRateMultiplier, BaseRateMultiplier>();
        services.AddScoped<IRateMultiplier, WeekendRateMultiplier>();
        services.AddScoped<IRateMultiplier, HolidayRateMultiplier>();
        services.AddScoped<IRateMultiplier, OvertimeRateMultiplier>();
        services.AddScoped<IProjectCostCalculator, ProjectCostCalculator>();
        services.AddScoped<IProjectTaskService, ProjectTaskService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<ICurrencyService, CurrencyService>();

        services.AddScoped<ICalendarProvider, GoogleCalendarProvider>();
        services.AddScoped<ICalendarProviderRegistry, CalendarProviderRegistry>();
        services.AddScoped<ITokenProtector, DataProtectionTokenProtector>();
        services.AddScoped<ICalendarIntegrationService, CalendarIntegrationService>();
        services.AddScoped<ICalendarSyncService, CalendarSyncService>();
        services.AddScoped<ICalendarViewService, CalendarViewService>();
        services.AddHostedService<CalendarSyncBackgroundService>();
        services.AddScoped<IJiraIntegrationService, JiraIntegrationService>();

        return services;
    }
}
