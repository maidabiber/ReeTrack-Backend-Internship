using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Application.Common.Models.CustomReports;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;

namespace ReeTrack.Infrastructure.Reports.Custom;

public sealed class CustomReportDefinitionService : ICustomReportDefinitionService
{
    private const int MaxNameLength = 120;
    private const int MaxDescriptionLength = 500;
    private const int MaxPageSize = 100;
    private const int MaxSpecJsonLength = 32 * 1024;
    private const int MaxDuplicateAttempts = 100;
    private const int CurrentSchemaVersion = 1;

    /// <summary>Reserves room for the longest suffix BuildCopyName can produce within MaxDuplicateAttempts.</summary>
    private const string LongestCopySuffix = " (copy 99)";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // jsonb does not preserve key order; polymorphic `type` may not be first on read-back.
        AllowOutOfOrderMetadataProperties = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public CustomReportDefinitionService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<CustomReportDefinitionDto>> ListAsync(
        int page = 1,
        int pageSize = 50,
        CustomReportOwnerFilter? ownerFilter = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var me = _currentUser.UserId;

        // Base visibility: every Shared definition, plus the caller's own Private ones.
        var query = _db.CustomReportDefinitions
            .AsNoTracking()
            .Where(definition => definition.Visibility == CustomReportVisibility.Shared
                || definition.CreatedByUserId == me);

        query = ownerFilter switch
        {
            CustomReportOwnerFilter.Mine => query.Where(definition => definition.CreatedByUserId == me),
            CustomReportOwnerFilter.Shared => query.Where(definition => definition.Visibility == CustomReportVisibility.Shared),
            _ => query
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(definition => definition.Name)
            .ThenBy(definition => definition.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomReportDefinitionDto>
        {
            Items = items.Select(definition => Map(definition)).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<CustomReportDefinitionDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var definition = await _db.CustomReportDefinitions
                .AsNoTracking()
                .FirstOrDefaultAsync(existing => existing.Id == id, cancellationToken)
            ?? throw AppErrors.NotFound("Custom report definition");

        // A Private report someone else owns doesn't exist as far as the caller is concerned —
        // a 403 would confirm it does.
        if (!CanView(definition))
            throw AppErrors.NotFound("Custom report definition");

        return Map(definition);
    }

    public async Task<CustomReportDefinitionDto> CreateAsync(
        string? name,
        string? description,
        CustomReportSpec spec,
        CustomReportVisibility visibility,
        CancellationToken cancellationToken = default)
    {
        var validatedName = ValidateName(name);
        var normalizedName = NormalizeName(validatedName);
        var validatedDescription = ValidateDescription(description);
        var validatedSpec = ValidateSpec(spec);
        var specJson = SerializeSpec(validatedSpec);
        var owner = _currentUser.UserId;

        await EnsureNameIsAvailableAsync(owner, normalizedName, excludeId: null, cancellationToken);

        var definition = new CustomReportDefinition
        {
            Name = validatedName,
            NormalizedName = normalizedName,
            Description = validatedDescription,
            SpecJson = specJson,
            SchemaVersion = CurrentSchemaVersion,
            CreatedByUserId = owner,
            Visibility = visibility
        };

        _db.CustomReportDefinitions.Add(definition);
        await SaveGuardingNameConflictAsync(cancellationToken);

        return Map(definition);
    }

    public async Task<CustomReportDefinitionDto> UpdateAsync(
        Guid id,
        string? name,
        string? description,
        CustomReportSpec spec,
        CustomReportVisibility visibility,
        CancellationToken cancellationToken = default)
    {
        var definition = await _db.CustomReportDefinitions
                .FirstOrDefaultAsync(existing => existing.Id == id, cancellationToken)
            ?? throw AppErrors.NotFound("Custom report definition");

        if (!CanView(definition))
            throw AppErrors.NotFound("Custom report definition");
        EnsureCanEdit(definition);

        var validatedName = ValidateName(name);
        var normalizedName = NormalizeName(validatedName);
        var validatedDescription = ValidateDescription(description);
        var validatedSpec = ValidateSpec(spec);
        var specJson = SerializeSpec(validatedSpec);

        await EnsureNameIsAvailableAsync(definition.CreatedByUserId, normalizedName, id, cancellationToken);

        definition.Name = validatedName;
        definition.NormalizedName = normalizedName;
        definition.Description = validatedDescription;
        definition.SpecJson = specJson;
        definition.SchemaVersion = CurrentSchemaVersion;
        definition.Visibility = visibility;

        await SaveGuardingNameConflictAsync(cancellationToken);
        return Map(definition);
    }

    public async Task<CustomReportDefinitionDto> DuplicateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var source = await _db.CustomReportDefinitions
                .AsNoTracking()
                .FirstOrDefaultAsync(existing => existing.Id == id, cancellationToken)
            ?? throw AppErrors.NotFound("Custom report definition");

        if (!CanView(source))
            throw AppErrors.NotFound("Custom report definition");

        // The copy is owned by whoever duplicated it, not the original author — so its name
        // only has to be unique among that person's own reports.
        var owner = _currentUser.UserId;
        var duplicateName = await ResolveDuplicateNameAsync(source.Name, owner, cancellationToken);

        var definition = new CustomReportDefinition
        {
            Name = duplicateName,
            NormalizedName = NormalizeName(duplicateName),
            Description = source.Description,
            SpecJson = source.SpecJson,
            SchemaVersion = source.SchemaVersion,
            CreatedByUserId = owner,
            Visibility = source.Visibility
        };

        _db.CustomReportDefinitions.Add(definition);
        await SaveGuardingNameConflictAsync(cancellationToken);

        return Map(definition);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var definition = await _db.CustomReportDefinitions
                .FirstOrDefaultAsync(existing => existing.Id == id, cancellationToken)
            ?? throw AppErrors.NotFound("Custom report definition");

        if (!CanView(definition))
            throw AppErrors.NotFound("Custom report definition");
        EnsureCanEdit(definition);

        definition.DeletedAtUtc = DateTime.UtcNow;
        definition.DeletedByUserId = _currentUser.UserId;

        await _db.SaveChangesAsync(cancellationToken);
    }

    private bool CanView(CustomReportDefinition definition) =>
        definition.Visibility == CustomReportVisibility.Shared
        || definition.CreatedByUserId == _currentUser.UserId;

    /// <summary>Only the creator may edit or delete — a Shared report is viewable by every admin, not writable.</summary>
    private void EnsureCanEdit(CustomReportDefinition definition)
    {
        if (definition.CreatedByUserId != _currentUser.UserId)
            throw AppErrors.Forbidden("Only the person who created this report can change it.");
    }

    private async Task EnsureNameIsAvailableAsync(
        Guid ownerId,
        string normalizedName,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var exists = await _db.CustomReportDefinitions
            .AsNoTracking()
            .AnyAsync(
                definition => definition.CreatedByUserId == ownerId
                    && definition.NormalizedName == normalizedName
                    && (excludeId == null || definition.Id != excludeId),
                cancellationToken);

        if (exists)
            throw AppErrors.Conflict("You already have a custom report with this name.");
    }

    /// <summary>
    /// One prefix query over this owner's names, then the first free "(copy)" / "(copy N)"
    /// suffix is picked in memory — instead of building up to 99 candidate strings up front and
    /// passing all of them into a single IN-list query.
    /// </summary>
    private async Task<string> ResolveDuplicateNameAsync(string sourceName, Guid ownerId, CancellationToken cancellationToken)
    {
        var normalizedPrefix = NormalizeName(SafeBasePrefix(sourceName));

        var takenNames = await _db.CustomReportDefinitions
            .AsNoTracking()
            .Where(definition => definition.CreatedByUserId == ownerId
                && definition.NormalizedName.StartsWith(normalizedPrefix))
            .Select(definition => definition.NormalizedName)
            .ToListAsync(cancellationToken);

        var takenSet = takenNames.ToHashSet(StringComparer.Ordinal);

        for (var copyNumber = 1; copyNumber < MaxDuplicateAttempts; copyNumber++)
        {
            var candidate = BuildCopyName(sourceName, copyNumber == 1 ? null : copyNumber);
            if (!takenSet.Contains(NormalizeName(candidate)))
                return candidate;
        }

        throw AppErrors.Conflict("Could not generate a unique name for the duplicate.");
    }

    private static string SafeBasePrefix(string name)
    {
        var maxBaseLength = MaxNameLength - LongestCopySuffix.Length;
        return name.Length > maxBaseLength ? name[..maxBaseLength].TrimEnd() : name;
    }

    private static string BuildCopyName(string name, int? copyNumber = null)
    {
        var suffix = copyNumber is null ? " (copy)" : $" (copy {copyNumber})";
        var maxBaseLength = MaxNameLength - suffix.Length;
        var trimmedBase = name.Length > maxBaseLength ? name[..maxBaseLength].TrimEnd() : name;
        return trimmedBase + suffix;
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
            throw AppErrors.Conflict("You already have a custom report with this name.");
        }
    }

    private static string ValidateName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw AppErrors.Validation("Custom report name is required.");

        if (trimmed.Length > MaxNameLength)
            throw AppErrors.Validation($"Custom report name cannot exceed {MaxNameLength} characters.");

        return trimmed;
    }

    private static string? ValidateDescription(string? description)
    {
        if (description is null)
            return null;

        var trimmed = description.Trim();
        if (trimmed.Length == 0)
            return null;

        if (trimmed.Length > MaxDescriptionLength)
            throw AppErrors.Validation($"Custom report description cannot exceed {MaxDescriptionLength} characters.");

        return trimmed;
    }

    private static CustomReportSpec ValidateSpec(CustomReportSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        CustomReportSpecValidator.Validate(spec);
        return spec;
    }

    private static string SerializeSpec(CustomReportSpec spec)
    {
        var json = JsonSerializer.Serialize(spec, JsonOptions);
        // The column stores UTF-8, so bound the encoded size, not the UTF-16 char count.
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxSpecJsonLength)
            throw AppErrors.Validation("The custom report spec is too large to save.");

        return json;
    }

    private static CustomReportSpec DeserializeSpec(string specJson) =>
        JsonSerializer.Deserialize<CustomReportSpec>(specJson, JsonOptions)
        ?? throw new InvalidOperationException("Stored custom report spec is invalid.");

    private CustomReportDefinitionDto Map(CustomReportDefinition definition) =>
        new()
        {
            Id = definition.Id,
            Name = definition.Name,
            Description = definition.Description,
            Spec = DeserializeSpec(definition.SpecJson),
            SchemaVersion = definition.SchemaVersion,
            CreatedByUserId = definition.CreatedByUserId,
            Visibility = definition.Visibility,
            CreatedAtUtc = definition.CreatedAtUtc,
            UpdatedAtUtc = definition.UpdatedAtUtc,
            CanEdit = definition.CreatedByUserId == _currentUser.UserId
        };

    private static string NormalizeName(string name) => name.ToUpperInvariant();
}
