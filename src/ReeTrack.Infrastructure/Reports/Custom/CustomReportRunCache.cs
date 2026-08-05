using Microsoft.Extensions.Caching.Memory;
using ReeTrack.Application.Common.Models.CustomReports;

namespace ReeTrack.Infrastructure.Reports.Custom;

/// <summary>
/// Short-lived cache of recently computed custom report runs, so operations derived from a
/// report the caller has already seen (export, insights) can reuse it instead of paying for
/// another full recompute.
/// </summary>
/// <remarks>
/// Owns a dedicated <see cref="MemoryCache"/> instance rather than the shared
/// <c>AddMemoryCache()</c> registration — nothing else in this codebase uses
/// <see cref="IMemoryCache"/>, and a dedicated instance keeps the entry-count limit scoped to
/// this feature instead of contending with a cache shared by unrelated code.
///
/// Keyed on user id as well as spec: <see cref="CustomReportDto.GeneratedByName"/> is specific
/// to the caller who ran the report, so a key shared across users would stamp one admin's name
/// on another admin's export. Report data itself isn't user-scoped (every endpoint here is
/// admin-only and applies no per-user filtering), so the user id is for attribution, not
/// authorisation.
/// </remarks>
public sealed class CustomReportRunCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private const int SizeLimit = 64;

    private readonly MemoryCache _cache = new(new MemoryCacheOptions { SizeLimit = SizeLimit });

    public bool TryGet(Guid userId, string cacheKey, out CustomReportDto report)
    {
        if (_cache.TryGetValue(Key(userId, cacheKey), out CustomReportDto? cached) && cached is not null)
        {
            report = cached;
            return true;
        }

        report = null!;
        return false;
    }

    public void Set(Guid userId, string cacheKey, CustomReportDto report)
    {
        _cache.Set(
            Key(userId, cacheKey),
            report,
            new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = Ttl,
                Size = 1
            });
    }

    private static string Key(Guid userId, string cacheKey) => $"{userId:N}:{cacheKey}";
}
