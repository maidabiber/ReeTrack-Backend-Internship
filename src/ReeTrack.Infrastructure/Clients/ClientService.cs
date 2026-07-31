using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Domain.Entities;

namespace ReeTrack.Infrastructure.Clients;

public class ClientService : IClientService
{
    private const int NameMaxLength = 200;

    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ClientService(IApplicationDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<PagedResult<ClientDto>> ListAsync(
        ClientListQuery query,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var filtered = _db.Clients.AsNoTracking();

        switch (query.Status?.Trim().ToLowerInvariant())
        {
            case null or "" or "active":
                filtered = filtered.Where(c => c.IsActive);
                break;
            case "archived":
                filtered = filtered.Where(c => !c.IsActive);
                break;
            case "all":
                break;
            default:
                throw new AppException("Status must be one of: active, archived, all.", 400, ErrorCode.StatusInvalid);
        }

        var q = query.Q?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(q))
            filtered = filtered.Where(c => c.Name.ToLower().Contains(q));

        var totalCount = await filtered.CountAsync(cancellationToken);

        var items = await filtered
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new ClientDto
            {
                Id = c.Id,
                Name = c.Name,
                IsActive = c.IsActive,
                ProjectCount = c.Projects.Count,
                CreatedAtUtc = c.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<ClientDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ClientDto> CreateAsync(string? name, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeName(name);
        await EnsureNameIsAvailableAsync(normalized, excludeId: null, cancellationToken);

        var client = new Client { Name = normalized };
        _db.Clients.Add(client);
        await SaveGuardingNameConflictAsync(cancellationToken);

        return MapClient(client, projectCount: 0);
    }

    public async Task<ClientDto> UpdateAsync(
        Guid id,
        string? name,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw AppErrors.NotFound("Client");

        if (name is not null)
        {
            var normalized = NormalizeName(name);
            if (!string.Equals(client.Name, normalized, StringComparison.Ordinal))
            {
                await EnsureNameIsAvailableAsync(normalized, excludeId: id, cancellationToken);
                client.Name = normalized;
            }
        }

        if (isActive.HasValue)
            client.IsActive = isActive.Value;

        await SaveGuardingNameConflictAsync(cancellationToken);

        var projectCount = await _db.Projects.CountAsync(p => p.ClientId == id, cancellationToken);
        return MapClient(client, projectCount);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
            ?? throw AppErrors.NotFound("Client");

        var hasProjects = await _db.Projects.AnyAsync(p => p.ClientId == id, cancellationToken);
        if (hasProjects)
            throw AppErrors.Conflict("This client has projects. Archive it instead.");

        client.DeletedAtUtc = DateTime.UtcNow;
        client.DeletedByUserId = _currentUser.UserId;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeName(string? name)
    {
        var trimmed = name?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            throw AppErrors.Validation("Client name is required.");
        if (trimmed.Length > NameMaxLength)
            throw AppErrors.Validation($"Client name must be at most {NameMaxLength} characters.");

        return trimmed;
    }

    private async Task EnsureNameIsAvailableAsync(
        string name,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var lowered = name.ToLower();
        var taken = await _db.Clients.AnyAsync(
            c => c.Name.ToLower() == lowered && (excludeId == null || c.Id != excludeId),
            cancellationToken);

        if (taken)
            throw AppErrors.Conflict("A client with this name already exists.");
    }

    // Backstop for the pre-check race: ix_clients_name is unique over non-deleted rows.
    private async Task SaveGuardingNameConflictAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw AppErrors.Conflict("A client with this name already exists.");
        }
    }

    internal static ClientDto MapClient(Client client, int projectCount) =>
        new()
        {
            Id = client.Id,
            Name = client.Name,
            IsActive = client.IsActive,
            ProjectCount = projectCount,
            CreatedAtUtc = client.CreatedAtUtc
        };
}
