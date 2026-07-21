using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Common.Interfaces;

public interface ITimesheetReviewService
{
    /// <summary>Admin queue, oldest submission first. Null status = all statuses.</summary>
    Task<PagedResult<AdminTimesheetListItemDto>> ListAsync(
        TimesheetStatus? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AdminTimesheetDetailDto> GetAsync(Guid timesheetId, CancellationToken cancellationToken = default);

    Task<TimesheetDto> ApproveAsync(Guid timesheetId, string? comment, CancellationToken cancellationToken = default);

    Task<TimesheetDto> RejectAsync(Guid timesheetId, string? comment, CancellationToken cancellationToken = default);
}
