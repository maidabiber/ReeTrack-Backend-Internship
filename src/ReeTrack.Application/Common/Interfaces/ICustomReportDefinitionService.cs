using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Models.CustomReports;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Common.Interfaces;

public interface ICustomReportDefinitionService
{
    /// <summary>
    /// Lists definitions visible to the caller — every Shared definition plus their own Private
    /// ones. <paramref name="ownerFilter"/> narrows further to just "mine" or just "shared";
    /// null returns everything visible.
    /// </summary>
    Task<PagedResult<CustomReportDefinitionDto>> ListAsync(
        int page = 1,
        int pageSize = 50,
        CustomReportOwnerFilter? ownerFilter = null,
        CancellationToken cancellationToken = default);

    Task<CustomReportDefinitionDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<CustomReportDefinitionDto> CreateAsync(
        string? name,
        string? description,
        CustomReportSpec spec,
        CustomReportVisibility visibility,
        CancellationToken cancellationToken = default);

    Task<CustomReportDefinitionDto> UpdateAsync(
        Guid id,
        string? name,
        string? description,
        CustomReportSpec spec,
        CustomReportVisibility visibility,
        CancellationToken cancellationToken = default);

    Task<CustomReportDefinitionDto> DuplicateAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public enum CustomReportOwnerFilter
{
    Mine,
    Shared
}
