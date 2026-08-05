using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReeTrack.Application.Common.Models.CustomReports;

namespace ReeTrack.Infrastructure.Reports.Custom;

/// <summary>
/// Hashes a <see cref="CustomReportSpec"/>'s identity for two different purposes that must
/// not be confused with each other — see the two methods below.
/// </summary>
internal static class CustomReportFingerprint
{
    private static readonly JsonSerializerOptions FingerprintJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>
    /// Identifies the shape of a report — filters and block configuration — for staleness
    /// detection. Narrative <see cref="NarrativeBlockSpec.CachedText"/> /
    /// <see cref="NarrativeBlockSpec.GeneratedAtUtc"/> are stripped first, otherwise storing
    /// generated commentary would immediately invalidate the fingerprint it was stored
    /// against.
    /// </summary>
    public static string Compute(CustomReportSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var material = new CustomReportSpec
        {
            Version = spec.Version,
            Query = spec.Query,
            Blocks = spec.Blocks.Select(Strip).ToList()
        };

        return Hash(material);
    }

    /// <summary>Exact spec identity, including narrative payloads — for run-result reuse.
    /// Distinct from Compute, which strips them for staleness detection: two specs differing
    /// only in stored narrative text must NOT collide here, or an export could be served with
    /// another report's cached commentary baked in
    /// (<see cref="BlockEvaluators.Evaluate"/> renders straight from
    /// <see cref="NarrativeBlockSpec.CachedText"/>).</summary>
    public static string ComputeCacheKey(CustomReportSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        return Hash(spec);
    }

    private static string Hash(CustomReportSpec spec)
    {
        var json = JsonSerializer.Serialize(spec, FingerprintJson);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }

    /// <summary>Removes the generated payload so a narrative block hashes by its config alone.</summary>
    private static ReportBlockSpec Strip(ReportBlockSpec block) =>
        block is NarrativeBlockSpec narrative
            ? new NarrativeBlockSpec
            {
                Id = narrative.Id,
                Title = narrative.Title,
                Focus = narrative.Focus
            }
            : block;
}
