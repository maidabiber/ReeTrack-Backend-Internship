using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Constants;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Domain.Services;
using ReeTrack.Infrastructure.Reports;

namespace ReeTrack.Infrastructure.Invoices;

public sealed class InvoiceService : IInvoiceService
{
    private const int MaxPageSize = 100;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IProjectCostCalculator _calculator;
    private readonly ReportEntryPipeline _pipeline;

    public InvoiceService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IProjectCostCalculator calculator,
        ReportEntryPipeline pipeline)
    {
        _db = db;
        _currentUser = currentUser;
        _calculator = calculator;
        _pipeline = pipeline;
    }

    private bool IsAdmin =>
        _currentUser.Roles.Contains(RoleNames.Admin, StringComparer.Ordinal);

    public async Task<InvoiceDto> GenerateAsync(
        GenerateInvoiceInput input,
        CancellationToken cancellationToken = default)
    {
        var query = input.Query;
        if (query.ClientIds.Count != 1)
            throw new AppException("Select exactly one client to generate an invoice.", 400);

        var client = await _db.Clients
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == query.ClientIds[0], cancellationToken)
            ?? throw new AppException("Client was not found.", 404);

        // ReportEntryPipeline already scopes non-Admin users to projects they created.
        var data = await _pipeline.LoadAsync(query, cancellationToken);
        var entries = data.Entries;
        var ratesByUser = data.UserRates.ToLookup(rate => rate.UserId);

        var projects = ProjectSummaryBuilder.Build(
            _calculator,
            entries,
            data.OvertimeContext,
            ratesByUser,
            data.Holidays,
            data.MultiplierConfig);

        var billableByProject = entries
            .Where(e => e.ProjectId is not null)
            .GroupBy(e => e.ProjectId!.Value)
            .ToDictionary(
                g => g.Key,
                g => g.Where(e => e.IsBillable).Sum(e => (long)e.DurationSeconds));

        var (projectRows, _) = ProfitabilityRollupBuilder.Build(projects, billableByProject);
        var lineItems = InvoiceLineBuilder.Build(projectRows);

        if (lineItems.Count == 0)
            throw new AppException("No billable project revenue in this period for the selected client.", 400);

        var currencies = projectRows
            .Where(p => p.Revenue > 0m)
            .Select(p => p.CurrencyCode)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (currencies.Count > 1)
            throw new AppException(
                "Cannot generate a single invoice across multiple currencies. Narrow the project filter.",
                400);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var periodFrom = data.Query.From
            ?? entries.Min(ReportMetadataResolver.ResolveEntryDate);
        var periodTo = data.Query.To ?? today;

        var invoice = new Invoice
        {
            Number = NewInvoiceNumber(),
            ClientId = client.Id,
            ClientName = client.Name,
            CurrencyCode = currencies[0],
            PeriodFrom = periodFrom,
            PeriodTo = periodTo,
            Subtotal = lineItems.Sum(line => line.Amount),
            Status = InvoiceStatus.Draft,
            GeneratedByUserId = _currentUser.UserId,
            LineItems = lineItems.ToList()
        };

        _db.Invoices.Add(invoice);
        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(invoice.Id, cancellationToken);
    }

    public async Task<PagedResult<InvoiceDto>> ListAsync(
        Guid? clientId = null,
        InvoiceStatus? status = null,
        string? search = null,
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = ApplyOwnershipFilter(_db.Invoices.AsNoTracking());

        if (clientId.HasValue)
            query = query.Where(invoice => invoice.ClientId == clientId.Value);

        if (status.HasValue)
            query = query.Where(invoice => invoice.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(invoice =>
                EF.Functions.ILike(invoice.Number, $"%{term}%") ||
                EF.Functions.ILike(invoice.ClientName, $"%{term}%") ||
                invoice.LineItems.Any(li => EF.Functions.ILike(li.Description, $"%{term}%")));

        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(invoice => invoice.LineItems)
            .OrderByDescending(invoice => invoice.CreatedAtUtc)
            .ThenByDescending(invoice => invoice.Number)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var clientIds = items.Select(invoice => invoice.ClientId).Distinct().ToList();
        var visibleInvoices = ApplyOwnershipFilter(_db.Invoices.AsNoTracking())
            .Where(i => clientIds.Contains(i.ClientId));

        var clientMaxDates = await visibleInvoices
            .GroupBy(i => i.ClientId)
            .Select(g => new { ClientId = g.Key, MaxDate = g.Max(x => x.CreatedAtUtc) })
            .ToListAsync(cancellationToken);

        var maxDates = clientMaxDates.Select(x => x.MaxDate).ToList();
        var newestInvoices = await visibleInvoices
            .Where(i => maxDates.Contains(i.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var newestInvoicesByClient = newestInvoices
            .GroupBy(i => i.ClientId)
            .ToDictionary(g => g.Key, g => g.First());

        var mappedItems = items.Select(invoice =>
        {
            newestInvoicesByClient.TryGetValue(invoice.ClientId, out var newest);
            var isSuperseded = newest != null && newest.CreatedAtUtc > invoice.CreatedAtUtc;
            return Map(invoice, isSuperseded ? newest!.Id : null, isSuperseded ? newest!.Number : null);
        }).ToList();

        return new PagedResult<InvoiceDto>
        {
            Items = mappedItems,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<InvoiceDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await _db.Invoices
                .AsNoTracking()
                .Include(i => i.LineItems)
                .FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new AppException("Invoice was not found.", 404);

        await EnsureCanAccessAsync(invoice, cancellationToken);

        var newerInvoice = await ApplyOwnershipFilter(_db.Invoices.AsNoTracking())
            .Where(i => i.ClientId == invoice.ClientId && i.CreatedAtUtc > invoice.CreatedAtUtc)
            .OrderBy(i => i.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return Map(invoice, newerInvoice?.Id, newerInvoice?.Number);
    }

    public async Task<InvoiceDto> MarkPaidAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await _db.Invoices
                .Include(i => i.LineItems)
                .FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new AppException("Invoice was not found.", 404);

        await EnsureCanAccessAsync(invoice, cancellationToken);

        if (invoice.Status != InvoiceStatus.Draft)
            throw new AppException("Only draft invoices can be marked as paid.", 409);

        invoice.Status = InvoiceStatus.Paid;
        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await _db.Invoices
                .Include(i => i.LineItems)
                .FirstOrDefaultAsync(i => i.Id == id, cancellationToken)
            ?? throw new AppException("Invoice was not found.", 404);

        await EnsureCanAccessAsync(invoice, cancellationToken);

        if (invoice.Status != InvoiceStatus.Draft)
            throw new AppException("Only draft invoices can be deleted.", 409);

        invoice.DeletedAtUtc = DateTime.UtcNow;
        invoice.DeletedByUserId = _currentUser.UserId;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ReportFile> ExportPdfAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await GetAsync(id, cancellationToken);
        return new PdfInvoiceWriter().Write(invoice);
    }

    /// <summary>
    /// Non-admins only see invoices whose every line item belongs to a project they created.
    /// </summary>
    private IQueryable<Invoice> ApplyOwnershipFilter(IQueryable<Invoice> query)
    {
        if (IsAdmin)
            return query;

        var userId = _currentUser.UserId;
        return query.Where(invoice =>
            invoice.LineItems.Any()
            && invoice.LineItems.All(li =>
                _db.Projects.Any(p => p.Id == li.ProjectId && p.CreatedByUserId == userId)));
    }

    private async Task EnsureCanAccessAsync(Invoice invoice, CancellationToken cancellationToken)
    {
        if (IsAdmin)
            return;

        var projectIds = invoice.LineItems.Select(li => li.ProjectId).Distinct().ToList();
        if (projectIds.Count == 0)
            throw AppErrors.Forbidden("You can only access invoices for projects you created.");

        var userId = _currentUser.UserId;
        var ownedCount = await _db.Projects.AsNoTracking()
            .CountAsync(
                p => projectIds.Contains(p.Id) && p.CreatedByUserId == userId,
                cancellationToken);

        if (ownedCount != projectIds.Count)
            throw AppErrors.Forbidden("You can only access invoices for projects you created.");
    }

    private static string NewInvoiceNumber() =>
        $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

    private static InvoiceDto Map(Invoice invoice, Guid? newerInvoiceId = null, string? newerInvoiceNumber = null) =>
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
            Status = invoice.Status,
            GeneratedByUserId = invoice.GeneratedByUserId,
            CreatedAtUtc = invoice.CreatedAtUtc,
            NewerInvoiceId = newerInvoiceId,
            NewerInvoiceNumber = newerInvoiceNumber,
            LineItems = invoice.LineItems
                .OrderBy(line => line.SortOrder)
                .ThenBy(line => line.Description, StringComparer.OrdinalIgnoreCase)
                .Select(line => new InvoiceLineItemDto
                {
                    Id = line.Id,
                    ProjectId = line.ProjectId,
                    Description = line.Description,
                    BillingModel = line.BillingModel,
                    Quantity = line.Quantity,
                    UnitPrice = line.UnitPrice,
                    Amount = line.Amount,
                    SortOrder = line.SortOrder
                })
                .ToList()
        };
}
