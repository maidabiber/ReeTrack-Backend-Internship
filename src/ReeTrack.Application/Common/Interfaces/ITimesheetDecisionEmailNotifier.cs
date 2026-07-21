using ReeTrack.Domain.Entities;

namespace ReeTrack.Application.Common.Interfaces;

public interface ITimesheetDecisionEmailNotifier
{
    /// <summary>Fire-and-forget decision email to the submitter; failures are logged, never thrown.</summary>
    void QueueDecisionEmail(Timesheet timesheet, User recipient, string reviewerName, bool approved);
}
