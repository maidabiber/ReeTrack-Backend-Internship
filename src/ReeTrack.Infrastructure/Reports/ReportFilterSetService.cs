using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Reports;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Reports;

public sealed class ReportFilterSetService : IReportFilterSetService
{
    private const int MaxNameLength = 100;
    private const int MaxPageSize = 100;
    private const int MaxQueryJsonLength = 8000;
    private const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ReportFilterSetService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<ReportFilterSetDto>> ListAsync(
        int page = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _db.ReportFilterSets
            .AsNoTracking()
            .Where(filterSet => filterSet.UserId == userId);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(filterSet => filterSet.Name)
            .ThenBy(filterSet => filterSet.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ReportFilterSetDto>
        {
            Items = items.Select(Map).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ReportFilterSetDto> CreateAsync(
        string? name,
        ReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var validatedName = ValidateName(name);
        var normalizedName = NormalizeName(validatedName);
        var userId = _currentUser.UserId;
        var validatedQuery = ReportQueryRules.NormalizeAndValidate(query);

        await EnsureNameIsAvailableAsync(userId, normalizedName, null, cancellationToken);

        var filterSet = new ReportFilterSet
        {
            UserId = userId,
            Name = validatedName,
            NormalizedName = normalizedName,
            QueryJson = SerializeQuery(validatedQuery),
            SchemaVersion = CurrentSchemaVersion
        };

        _db.ReportFilterSets.Add(filterSet);
        await SaveGuardingNameConflictAsync(cancellationToken);

        return Map(filterSet);
    }

    public async Task<ReportFilterSetDto> UpdateAsync(
        Guid id,
        string? name,
        ReportQuery query,
        CancellationToken cancellationToken = default)
    {
        var validatedName = ValidateName(name);
        var normalizedName = NormalizeName(validatedName);
        var userId = _currentUser.UserId;
        var validatedQuery = ReportQueryRules.NormalizeAndValidate(query);

        var filterSet = await _db.ReportFilterSets
                .FirstOrDefaultAsync(
                    existing => existing.Id == id && existing.UserId == userId,
                    cancellationToken)
            ?? throw AppErrors.NotFound("Report filter set");

        await EnsureNameIsAvailableAsync(userId, normalizedName, id, cancellationToken);

        filterSet.Name = validatedName;
        filterSet.NormalizedName = normalizedName;
        filterSet.QueryJson = SerializeQuery(validatedQuery);
        filterSet.SchemaVersion = CurrentSchemaVersion;

        await SaveGuardingNameConflictAsync(cancellationToken);
        return Map(filterSet);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.UserId;
        var filterSet = await _db.ReportFilterSets
                .FirstOrDefaultAsync(
                    existing => existing.Id == id && existing.UserId == userId,
                    cancellationToken)
            ?? throw AppErrors.NotFound("Report filter set");

        _db.ReportFilterSets.Remove(filterSet);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureNameIsAvailableAsync(
        Guid userId,
        string normalizedName,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var exists = await _db.ReportFilterSets
            .AsNoTracking()
            .AnyAsync(
                filterSet => filterSet.UserId == userId
                    && filterSet.NormalizedName == normalizedName
                    && (excludeId == null || filterSet.Id != excludeId),
                cancellationToken);

        if (exists)
            throw AppErrors.Conflict("A report filter set with this name already exists.");
    }

    private async Task SaveGuardingNameConflictAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw AppErrors.Conflict("A report filter set with this name already exists.");
        }
    }

    private static string ValidateName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw AppErrors.Validation("Report filter set name is required.");

        if (trimmed.Length > MaxNameLength)
            throw AppErrors.Validation($"Report filter set name cannot exceed {MaxNameLength} characters.");

        return trimmed;
    }

    private static string NormalizeName(string name) => name.ToUpperInvariant();

    private static string SerializeQuery(ReportQuery query)
    {
        var json = JsonSerializer.Serialize(query, JsonOptions);
        if (json.Length > MaxQueryJsonLength)
            throw AppErrors.Validation("The report filter set is too large to save.");

        return json;
    }

    private static ReportFilterSetDto Map(ReportFilterSet filterSet)
    {
        var query = JsonSerializer.Deserialize<ReportQuery>(filterSet.QueryJson, JsonOptions)
            ?? throw new InvalidOperationException("Stored report filter query is invalid.");

        return new ReportFilterSetDto
        {
            Id = filterSet.Id,
            Name = filterSet.Name,
            Query = query,
            SchemaVersion = filterSet.SchemaVersion,
            CreatedAtUtc = filterSet.CreatedAtUtc,
            UpdatedAtUtc = filterSet.UpdatedAtUtc
        };
    }
}
