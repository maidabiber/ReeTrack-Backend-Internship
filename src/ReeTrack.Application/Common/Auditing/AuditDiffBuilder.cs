using System.Text.Json;
using ReeTrack.Domain.Common;

namespace ReeTrack.Application.Common.Auditing;

public sealed record AuditDiff(string? OldValuesJson, string? NewValuesJson);

/// <summary>
/// Pure diff/serialization logic for audit rows. No EF dependencies so it is
/// unit testable; the interceptor feeds it property snapshots.
/// </summary>
public static class AuditDiffBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase
    };

    // Excluded from update diffs: derivable from the audit row's own timestamp
    // and would otherwise turn every save into a noise row.
    private static readonly HashSet<string> TimestampProperties =
        [nameof(BaseEntity.CreatedAtUtc), nameof(BaseEntity.UpdatedAtUtc)];

    public static AuditDiff BuildForCreate(string entityType, IReadOnlyList<AuditPropertySnapshot> properties) =>
        new(null, Serialize(entityType, properties, p => p.CurrentValue));

    public static AuditDiff BuildForDelete(string entityType, IReadOnlyList<AuditPropertySnapshot> properties) =>
        new(Serialize(entityType, properties, p => p.OriginalValue), null);

    // Soft delete: full snapshot on the old side so the data is reconstructable,
    // only the delete markers (DeletedAtUtc/DeletedByUserId) on the new side.
    public static AuditDiff BuildForSoftDelete(string entityType, IReadOnlyList<AuditPropertySnapshot> properties) =>
        new(Serialize(entityType, properties, p => p.OriginalValue),
            Serialize(entityType, ChangedNonTimestamp(properties), p => p.CurrentValue));

    public static AuditDiff BuildForRestore(string entityType, IReadOnlyList<AuditPropertySnapshot> properties) =>
        new(Serialize(entityType, ChangedNonTimestamp(properties), p => p.OriginalValue),
            Serialize(entityType, properties, p => p.CurrentValue));

    /// <summary>Returns null when nothing but timestamps changed — the caller writes no audit row.</summary>
    public static AuditDiff? BuildForUpdate(string entityType, IReadOnlyList<AuditPropertySnapshot> properties)
    {
        var changed = ChangedNonTimestamp(properties);
        if (changed.Count == 0)
            return null;

        return new AuditDiff(
            Serialize(entityType, changed, p => p.OriginalValue),
            Serialize(entityType, changed, p => p.CurrentValue));
    }

    private static List<AuditPropertySnapshot> ChangedNonTimestamp(IReadOnlyList<AuditPropertySnapshot> properties) =>
        properties
            .Where(p => p.IsModified
                && !TimestampProperties.Contains(p.Name)
                && !Equals(p.OriginalValue, p.CurrentValue))
            .ToList();

    private static string Serialize(
        string entityType,
        IReadOnlyList<AuditPropertySnapshot> properties,
        Func<AuditPropertySnapshot, object?> valueSelector)
    {
        var values = new Dictionary<string, object?>();
        foreach (var property in properties)
        {
            values[property.Name] = AuditRedaction.IsSensitive(entityType, property.Name)
                ? AuditRedaction.Mask
                : NormalizeValue(valueSelector(property));
        }

        return JsonSerializer.Serialize(values, JsonOptions);
    }

    // Audit logs are read by humans months later: enums as names, dates in ISO 8601.
    private static object? NormalizeValue(object? value) => value switch
    {
        null => null,
        Enum e => e.ToString(),
        DateTime dt => dt.ToString("O"),
        Guid g => g.ToString(),
        byte[] bytes => Convert.ToBase64String(bytes),
        _ => value
    };
}
