using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Api.Mapping;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/invoices")]
[Authorize(Roles = "Admin")]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _invoices;

    public InvoicesController(IInvoiceService invoices)
    {
        _invoices = invoices;
    }

    /// <summary>RT-209 — generate a draft client invoice from billable time filters.</summary>
    [HttpPost("generate")]
    public async Task<ActionResult<InvoiceResponse>> Generate(
        [FromBody] GenerateInvoiceRequest? request,
        CancellationToken cancellationToken)
    {
        if (request?.Query is null)
            throw new AppException("Invoice filter query is required.", 400);

        var invoice = await _invoices.GenerateAsync(
            new GenerateInvoiceInput { Query = ReportQueryMapping.FromRequest(request.Query) },
            cancellationToken);

        return Ok(Map(invoice));
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<InvoiceResponse>>> List(
        [FromQuery] Guid? clientId = null,
        [FromQuery] InvoiceStatus? status = null,
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var result = await _invoices.ListAsync(clientId, status, q, page, pageSize, cancellationToken);
        return Ok(new PagedResult<InvoiceResponse>
        {
            Items = result.Items.Select(Map).ToList(),
            TotalCount = result.TotalCount,
            Page = result.Page,
            PageSize = result.PageSize
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<InvoiceResponse>> Get(
        Guid id,
        CancellationToken cancellationToken)
    {
        var invoice = await _invoices.GetAsync(id, cancellationToken);
        return Ok(Map(invoice));
    }

    /// <summary>RT-210 — download the invoice as a PDF.</summary>
    [HttpGet("{id:guid}/pdf")]
    public async Task<IActionResult> ExportPdf(Guid id, CancellationToken cancellationToken)
    {
        var file = await _invoices.ExportPdfAsync(id, cancellationToken);
        return File(file.Bytes, file.ContentType, file.FileName);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _invoices.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/mark-paid")]
    public async Task<ActionResult<InvoiceResponse>> MarkPaid(Guid id, CancellationToken cancellationToken)
    {
        var invoice = await _invoices.MarkPaidAsync(id, cancellationToken);
        return Ok(Map(invoice));
    }

    private static InvoiceResponse Map(InvoiceDto invoice) =>
        new()
        {
            Id = invoice.Id,
            Number = invoice.Number,
            ClientId = invoice.ClientId,
            ClientName = invoice.ClientName,
            CurrencyCode = invoice.CurrencyCode,
            PeriodFrom = invoice.PeriodFrom,
            PeriodTo = invoice.PeriodTo,
            Subtotal = invoice.Subtotal,
            Status = invoice.Status.ToString(),
            GeneratedByUserId = invoice.GeneratedByUserId,
            CreatedAtUtc = invoice.CreatedAtUtc,
            NewerInvoiceId = invoice.NewerInvoiceId,
            NewerInvoiceNumber = invoice.NewerInvoiceNumber,
            LineItems = invoice.LineItems.Select(MapLine).ToList()
        };

    private static InvoiceLineItemResponse MapLine(InvoiceLineItemDto line) =>
        new()
        {
            Id = line.Id,
            ProjectId = line.ProjectId,
            Description = line.Description,
            BillingModel = line.BillingModel switch
            {
                InvoiceLineBillingModel.FixedFee => "FixedFee",
                _ => "Hourly"
            },
            Quantity = line.Quantity,
            UnitPrice = line.UnitPrice,
            Amount = line.Amount,
            SortOrder = line.SortOrder
        };
}
