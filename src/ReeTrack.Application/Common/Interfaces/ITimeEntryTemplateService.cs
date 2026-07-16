using ReeTrack.Application.Common.Models;

namespace ReeTrack.Application.Common.Interfaces;

public interface ITimeEntryTemplateService
{
    Task<PagedResult<TimeEntryTemplateDto>> ListAsync(
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<TimeEntryTemplateDto> CreateFromTimeEntryAsync(
        Guid timeEntryId,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
