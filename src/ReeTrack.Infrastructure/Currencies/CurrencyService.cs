using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using ReeTrack.Application.Common.Exceptions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Infrastructure.Currencies;

public sealed class CurrencyService : ICurrencyService
{
    private const string DefaultCode = "EUR";

    private readonly IApplicationDbContext _db;

    public CurrencyService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<CurrencyDto>> ListActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Currencies
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Code)
            .Select(c => new CurrencyDto
            {
                Code = c.Code,
                Name = c.Name
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<string> EnsureSupportedAsync(
    string? currencyCode,
    CancellationToken cancellationToken = default)
    {
        var trimmed = currencyCode?.Trim();
        var code = string.IsNullOrEmpty(trimmed) ? DefaultCode : trimmed.ToUpperInvariant();

        if (code.Length != 3)
            throw new AppException("Invalid currency code length.");

        var isActive = await _db.Currencies
            .AsNoTracking()
            .AnyAsync(c => c.Code == code && c.IsActive, cancellationToken);

        if (!isActive)
            throw new AppException($"Currency '{code}' is not supported or inactive in the system.");

        return code;
    }
}
