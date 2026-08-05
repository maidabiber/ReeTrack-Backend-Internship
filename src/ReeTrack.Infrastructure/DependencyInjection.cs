using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenAI;
using ReeTrack.Application.Calendar;
using System.ClientModel;
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
using ReeTrack.Infrastructure.Reports.Custom;
using ReeTrack.Infrastructure.Reports.Custom.Insights;
using ReeTrack.Infrastructure.Reports.Writers;
using ReeTrack.Infrastructure.Reports.Writers.Custom;
using ReeTrack.Infrastructure.Holidays;
using ReeTrack.Infrastructure.Tags;
using ReeTrack.Infrastructure.Teammates;
using ReeTrack.Infrastructure.TimeEntries;
using ReeTrack.Infrastructure.Timesheets;
using ReeTrack.Infrastructure.SmartTimeParse;
using ReeTrack.Infrastructure.Assistant;
using ReeTrack.Infrastructure.UserHourlyRates;
using ReeTrack.Infrastructure.Background;
using ReeTrack.Infrastructure.Calendar;
using ReeTrack.Infrastructure.Integrations.Calendar;
using ReeTrack.Infrastructure.Integrations.Calendar.Google;
using ReeTrack.Infrastructure.Integrations.Jira;
using ReeTrack.Infrastructure.Integrations.Slack;
using ReeTrack.Application.Integrations.Jira;
using ReeTrack.Application.Integrations.Slack;
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
        services.Configure<ReportOptions>(configuration.GetSection(ReportOptions.SectionName));
        services.Configure<LlmOptions>(configuration.GetSection(LlmOptions.SectionName));
        services.Configure<JiraOptions>(configuration.GetSection(JiraOptions.SectionName));
        services.Configure<SlackOptions>(configuration.GetSection(SlackOptions.SectionName));

        // Register IChatClient using Microsoft.Extensions.AI + OpenAI adapter.
        // Always register so DI validation passes; the actual call will fail with a clear
        // error at request time if the key is missing.
        services.AddSingleton<IChatClient>(sp =>
        {
            var llmOptions = configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>() ?? new LlmOptions();
            var apiKey = Environment.GetEnvironmentVariable("GROQ_API_KEY") ?? llmOptions.ApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = "not-configured";
            }
            var openAiClient = new OpenAIClient(
                new ApiKeyCredential(apiKey),
                new OpenAIClientOptions { Endpoint = new Uri(llmOptions.BaseUrl) });
            var chatClient = openAiClient.GetChatClient(llmOptions.Model);
            var innerClient = chatClient.AsIChatClient();
            return innerClient
                .AsBuilder()
                .UseFunctionInvocation(sp.GetRequiredService<ILoggerFactory>())
                .Build(sp);
        });

        services.AddHttpContextAccessor();
        services.Configure<CalendarSyncOptions>(configuration.GetSection(CalendarSyncOptions.SectionName));
        services.Configure<WeeklyTargetCheckInOptions>(configuration.GetSection(WeeklyTargetCheckInOptions.SectionName));

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
        services.AddHttpClient<ISlackApiClient, SlackApiClient>((sp, client) =>
        {
            var slack = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<SlackOptions>>().Value;
            var baseUrl = string.IsNullOrWhiteSpace(slack.BaseUrl)
                ? "https://slack.com/api/"
                : slack.BaseUrl;
            if (!baseUrl.EndsWith('/'))
                baseUrl += "/";
            client.BaseAddress = new Uri(baseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddScoped<IGoogleOAuthService, GoogleOAuthService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISetupService, SetupService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        services.AddScoped<ITransactionalEmailSender, TransactionalEmailSender>();
        services.AddScoped<IDomainEventPublisher, DomainEventPublisher>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();
        services.AddScoped<INotificationPreferenceService, NotificationPreferenceService>();
        services.AddScoped<IInAppNotificationService, InAppNotificationService>();
        services.AddScoped<IChannelProvider, EmailChannelProvider>();
        services.AddScoped<IChannelProvider, InAppChannelProvider>();
        services.AddScoped<IChannelProvider, SlackChannelProvider>();
        services.AddScoped<ISlackIntegrationService, SlackIntegrationService>();
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
        services.AddScoped<IDailyTimeBudget, DailyTimeBudget>();
        services.AddScoped<ITimeEntryOverlapChecker, TimeEntryOverlapChecker>();
        services.AddScoped<ITimeEntryTemplateService, TimeEntryTemplateService>();
        services.AddScoped<ISmartTimeParseService, SmartTimeParseService>();
        services.AddScoped<AssistantTools>();
        services.AddScoped<IAssistantService, AssistantService>();
        services.AddScoped<ITimeEntrySuggestionService, TimeEntrySuggestionService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<IProjectCostService, ProjectCostService>();
        services.AddScoped<ReportEntryPipeline>();
        services.AddScoped<IReportService, ReportService>();
        services.AddSingleton<CustomReportRunCache>();
        services.AddScoped<ICustomReportService, CustomReportService>();
        services.AddScoped<ICustomReportDefinitionService, CustomReportDefinitionService>();
        services.AddScoped<ICustomReportInsightService, CustomReportInsightService>();
        services.AddScoped<IReportWriter<Application.Common.Models.CustomReports.CustomReportDto>, CustomCsvReportWriter>();
        services.AddScoped<IReportWriter<Application.Common.Models.CustomReports.CustomReportDto>, CustomExcelReportWriter>();
        services.AddScoped<IReportWriter<Application.Common.Models.CustomReports.CustomReportDto>, CustomPdfReportWriter>();
        services.AddScoped<IReportFilterSetService, ReportFilterSetService>();
        services.AddScoped<IInvoiceService, Invoices.InvoiceService>();
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
        services.AddScoped<IWeeklyTargetCheckInJob, WeeklyTargetCheckInJob>();
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
        services.AddHostedService<WeeklyTargetCheckInBackgroundService>();
        services.AddScoped<IJiraIntegrationService, JiraIntegrationService>();

        return services;
    }
}
