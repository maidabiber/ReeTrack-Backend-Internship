using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Tags;

public class TagService : ITagService
{
    private const int NameMaxLength = 100;

    private static readonly Regex ColorPattern = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public TagService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<TagDto>> ListAsync(
        TagListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var filtered = _db.Tags.AsNoTracking();

        var q = query.Q?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(q))
            filtered = filtered.Where(t => t.Name.ToLower().Contains(q));

        var totalCount = await filtered.CountAsync(cancellationToken);

        var items = await filtered
            .OrderBy(t => t.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new TagDto
            {
                Id = t.Id,
                Name = t.Name,
                Color = t.Color,
                UsageCount = t.TimeEntryTags.Count,
                CreatedAtUtc = t.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<TagDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<TagDto> CreateAsync(
        string? name,
        string? color,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = NormalizeName(name);
        var normalizedColor = NormalizeColor(color);
        await EnsureNameIsAvailableAsync(normalizedName, excludeId: null, cancellationToken);

        var tag = new Tag { Name = normalizedName, Color = normalizedColor };
        _db.Tags.Add(tag);
        await SaveGuardingNameConflictAsync(cancellationToken);

        return MapTag(tag, usageCount: 0);
    }

    public async Task<TagDto> UpdateAsync(
        Guid id,
        string? name,
        string? color,
        CancellationToken cancellationToken = default)
    {
        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new AppException("Tag was not found.", 404);

        if (name is not null)
        {
            var normalized = NormalizeName(name);
            if (!string.Equals(tag.Name, normalized, StringComparison.Ordinal))
            {
                await EnsureNameIsAvailableAsync(normalized, excludeId: id, cancellationToken);
                tag.Name = normalized;
            }
        }

        // Sentinel: null leaves the color unchanged; an empty string clears it.
        if (color is not null)
            tag.Color = NormalizeColor(color);

        await SaveGuardingNameConflictAsync(cancellationToken);

        var usageCount = await _db.TimeEntryTags.CountAsync(t => t.TagId == id, cancellationToken);
        return MapTag(tag, usageCount);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tag = await _db.Tags.FirstOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? throw new AppException("Tag was not found.", 404);

        // Tags may be deleted even while in use: the soft-delete keeps historical
        // time-entry associations intact and the filtered name index lets the name
        // be reused immediately.
        tag.DeletedAtUtc = DateTime.UtcNow;
        tag.DeletedByUserId = _currentUser.UserId;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw new AppException("Tag name is required.");
        if (trimmed.Length > NameMaxLength)
            throw new AppException($"Tag name must be at most {NameMaxLength} characters.");

        return trimmed;
    }

    private static string? NormalizeColor(string? color)
    {
        var trimmed = color?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return null;
        if (!ColorPattern.IsMatch(trimmed))
            throw new AppException("Color must be a hex value like #4366E2.");

        return trimmed.ToUpperInvariant();
    }

    private async Task EnsureNameIsAvailableAsync(
        string name,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var lowered = name.ToLower();
        var taken = await _db.Tags.AnyAsync(
            t => t.Name.ToLower() == lowered && (excludeId == null || t.Id != excludeId),
            cancellationToken);

        if (taken)
            throw new AppException("A tag with this name already exists.", 409);
    }

    // Backstop for the pre-check race: ix_tags_name is unique over non-deleted rows.
    private async Task SaveGuardingNameConflictAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new AppException("A tag with this name already exists.", 409);
        }
    }

    private static TagDto MapTag(Tag tag, int usageCount) =>
        new()
        {
            Id = tag.Id,
            Name = tag.Name,
            Color = tag.Color,
            UsageCount = usageCount,
            CreatedAtUtc = tag.CreatedAtUtc
        };
}
