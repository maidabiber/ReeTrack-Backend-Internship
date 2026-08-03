using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Application.Common.Interfaces;

public interface IInvoiceService
{
    Task<InvoiceDto> GenerateAsync(
        GenerateInvoiceInput input,
        CancellationToken cancellationToken = default);

    Task<PagedResult<InvoiceDto>> ListAsync(
        Guid? clientId = null,
        InvoiceStatus? status = null,
        string? search = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default);

    Task<InvoiceDto> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Marks a draft invoice as paid; terminal state.</summary>
    Task<InvoiceDto> MarkPaidAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a draft invoice; admins only.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>RT-210 — QuestPDF export of a persisted invoice.</summary>
    Task<ReportFile> ExportPdfAsync(Guid id, CancellationToken cancellationToken = default);
}
