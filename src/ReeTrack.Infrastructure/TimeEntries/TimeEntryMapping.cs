using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.TimeEntries;

internal static class TimeEntryMapping
{
    public static TimeEntryDto MapEntity(
        TimeEntry entry,
        string? submittedByDisplayName = null,
        string? assigneeDisplayName = null,
        IReadOnlyDictionary<Guid, List<TimeEntry>>? shareGroups = null) =>
        new()
        {
            Id = entry.Id,
            Description = entry.Description,
            IsBillable = entry.IsBillable,
            Mode = entry.Mode.ToString(),
            StartedAtUtc = entry.StartedAtUtc,
            EndedAtUtc = entry.EndedAtUtc,
            DurationSeconds = entry.DurationSeconds,
            IsRunning = entry.Mode == TimeEntryMode.Timer && entry.EndedAtUtc is null,
            Status = entry.Status.ToString(),
            SubmittedByUserId = entry.SubmittedByUserId,
            SubmittedByDisplayName = submittedByDisplayName,
            AssigneeUserId = entry.UserId,
            AssigneeDisplayName = assigneeDisplayName,
            ShareGroupId = entry.ShareGroupId,
            Participants = shareGroups is null ? [] : BuildParticipants(entry, shareGroups)
        };

    private static IReadOnlyList<TimeEntryParticipantDto> BuildParticipants(
        TimeEntry entry,
        IReadOnlyDictionary<Guid, List<TimeEntry>> shareGroups)
    {
        var participants = new List<TimeEntryParticipantDto>();
        var seenUserIds = new HashSet<Guid>();

        void AddParticipant(Guid userId, string displayName, string email, string role)
        {
            if (!seenUserIds.Add(userId))
                return;

            participants.Add(new TimeEntryParticipantDto
            {
                UserId = userId,
                DisplayName = displayName,
                Email = email,
                Role = role
            });
        }

        if (entry.SubmittedByUser is { } submitter && entry.SubmittedByUserId is Guid submitterId)
        {
            AddParticipant(
                submitterId,
                submitter.DisplayName?.Trim() ?? submitter.Email,
                submitter.Email,
                "Submitter");
        }
        else if (entry.ShareGroupId is not null && entry.SubmittedByUserId is null)
        {
            AddParticipant(
                entry.UserId,
                entry.User.DisplayName?.Trim() ?? entry.User.Email,
                entry.User.Email,
                "Submitter");
        }

        if (entry.ShareGroupId is Guid groupId && shareGroups.TryGetValue(groupId, out var siblings))
        {
            foreach (var sibling in siblings.Where(sibling => sibling.SubmittedByUserId is not null))
            {
                AddParticipant(
                    sibling.UserId,
                    sibling.User.DisplayName?.Trim() ?? sibling.User.Email,
                    sibling.User.Email,
                    "Assignee");
            }
        }
        else if (entry.SubmittedByUserId is not null)
        {
            AddParticipant(
                entry.UserId,
                entry.User.DisplayName?.Trim() ?? entry.User.Email,
                entry.User.Email,
                "Assignee");
        }

        return participants;
    }
}
