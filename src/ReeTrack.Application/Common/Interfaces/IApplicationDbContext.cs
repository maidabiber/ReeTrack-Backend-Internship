using Microsoft.EntityFrameworkCore;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<Invitation> Invitations { get; }
    DbSet<Client> Clients { get; }
    DbSet<Project> Projects { get; }
    DbSet<ProjectTask> ProjectTasks { get; }
    DbSet<Tag> Tags { get; }
    DbSet<TimeEntry> TimeEntries { get; }
    DbSet<TimeEntryTemplate> TimeEntryTemplates { get; }
    DbSet<TimeEntryTemplateTag> TimeEntryTemplateTags { get; }
    DbSet<TimeEntryTag> TimeEntryTags { get; }
    DbSet<Timesheet> Timesheets { get; }
    DbSet<UserCalendarConnection> UserCalendarConnections { get; }
    DbSet<SyncedCalendarEvent> SyncedCalendarEvents { get; }
    DbSet<UserHourlyRate> UserHourlyRates { get; }
    DbSet<Currency> Currencies { get; }
    DbSet<ProjectCostSnapshot> ProjectCostSnapshots { get; }
    DbSet<Holiday> Holidays { get; }
    DbSet<HolidayCalendarSettings> HolidayCalendarSettings { get; }
    DbSet<RateMultiplierSettings> RateMultiplierSettings { get; }
    DbSet<JiraWebhookSettings> JiraWebhookSettings { get; }
    DbSet<HourTargetSettings> HourTargetSettings { get; }
    DbSet<UserHourTarget> UserHourTargets { get; }
    DbSet<WeeklyTargetCheckInRun> WeeklyTargetCheckInRuns { get; }
    DbSet<NotificationPreference> NotificationPreferences { get; }
    DbSet<ReportFilterSet> ReportFilterSets { get; }
    DbSet<CustomReportDefinition> CustomReportDefinitions { get; }
    DbSet<InAppNotification> InAppNotifications { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceLineItem> InvoiceLineItems { get; }
    DbSet<ReportShareLink> ReportShareLinks { get; }
    DbSet<ReportShareRecipient> ReportShareRecipients { get; }
    DbSet<ProjectThreshold> ProjectThresholds { get; }
    DbSet<PendingProjectAlert> PendingProjectAlerts { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
