using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Services;

namespace ReeTrack.Infrastructure.Reports;

/// <summary>Maps one time entry (plus its calculated cost line, if any) to the detailed report row.</summary>
internal static class DetailedEntryMapper
{
    public static DetailedEntryDto Map(TimeEntry entry, EntryCostLine? cost)
    {
        var user = entry.User;
        var displayName = string.IsNullOrWhiteSpace(user?.DisplayName)
            ? user?.Email ?? entry.UserId.ToString()
            : user.DisplayName;

        var emptyCost = cost is null;
        return new DetailedEntryDto
        {
            EntryId = entry.Id,
            EntryDate = ReportMetadataResolver.ResolveEntryDate(entry),
            StartedAtUtc = entry.StartedAtUtc,
            EndedAtUtc = entry.EndedAtUtc,
            UserId = entry.UserId,
            DisplayName = displayName,
            ClientId = ReportMetadataResolver.ResolveClientId(entry),
            ClientName = ReportMetadataResolver.ResolveClientName(entry),
            ProjectId = entry.ProjectId,
            ProjectName = entry.Project?.Name,
            TaskId = entry.ProjectTaskId,
            TaskName = entry.ProjectTask?.Name,
            Tags = entry.TimeEntryTags
                .Where(t => t.Tag is not null && t.Tag.DeletedAtUtc is null)
                .Select(t => t.Tag.Name)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            Description = entry.Description,
            IsBillable = entry.IsBillable,
            DurationSeconds = entry.DurationSeconds,
            CurrencyCode = entry.Project?.CurrencyCode,
            CalculatedCost = emptyCost ? 0m : ReportRounding.Cost(cost!.CalculatedCost),
            NormalCost = emptyCost ? 0m : ReportRounding.Cost(cost!.NormalCost),
            WeekendCost = emptyCost ? 0m : ReportRounding.Cost(cost!.WeekendCost),
            HolidayCost = emptyCost ? 0m : ReportRounding.Cost(cost!.HolidayCost),
            OvertimeCost = emptyCost ? 0m : ReportRounding.Cost(cost!.OvertimeCost),
            OvertimeHours = emptyCost ? 0m : ReportRounding.Hours(cost!.OvertimeHours),
            WeekendHours = emptyCost ? 0m : ReportRounding.Hours(cost!.WeekendHours),
            HolidayHours = emptyCost ? 0m : ReportRounding.Hours(cost!.HolidayHours),
            IsWeekend = cost?.IsWeekend
                ?? ReportMetadataResolver.ResolveEntryDate(entry).DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
            IsHoliday = cost?.IsHoliday ?? false
        };
    }
}
