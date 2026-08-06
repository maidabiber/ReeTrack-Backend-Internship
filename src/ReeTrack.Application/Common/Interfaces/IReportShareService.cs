using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Common.Interfaces;

public interface IReportShareService
{
    Task<Guid> GenerateLinkAsync(
        CreateShareLinkRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShareLinkDto>> FetchLinksAsync(
        ReportShareReportType reportType,
        CancellationToken cancellationToken = default);

    Task RemoveLinkAsync(
        Guid shareLinkId,
        CancellationToken cancellationToken = default);

    Task<SharedReportDto> GetSharedReportAsync(
        string token,
        CancellationToken cancellationToken = default);
}
