using System.Text.Json;
using ReeTrack.Application.Common.Auditing;
using ReeTrack.Domain.Enums;
using Xunit;

namespace ReeTrack.UnitTests.Auditing;

public class AuditDiffBuilderTests
{
    private static AuditPropertySnapshot Unchanged(string name, object? value) =>
        new(name, value, value, IsModified: false);

    private static AuditPropertySnapshot Changed(string name, object? oldValue, object? newValue) =>
        new(name, oldValue, newValue, IsModified: true);

    private static Dictionary<string, JsonElement> Parse(string? json)
    {
        Assert.NotNull(json);
        return JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json!)!;
    }

    [Fact]
    public void BuildForCreate_HasNullOldSide_AndFullNewSnapshot()
    {
        var id = Guid.NewGuid();
        var startedAt = new DateTime(2026, 7, 7, 9, 30, 0, DateTimeKind.Utc);

        var diff = AuditDiffBuilder.BuildForCreate("TimeEntry",
        [
            new AuditPropertySnapshot("Id", null, id, false),
            new AuditPropertySnapshot("Description", null, "standup", false),
            new AuditPropertySnapshot("Mode", null, TimeEntryMode.Timer, false),
            new AuditPropertySnapshot("StartedAtUtc", null, startedAt, false),
            new AuditPropertySnapshot("DurationSeconds", null, 900, false),
        ]);

        Assert.Null(diff.OldValuesJson);
        var newValues = Parse(diff.NewValuesJson);
        Assert.Equal(id.ToString(), newValues["id"].GetString());
        Assert.Equal("standup", newValues["description"].GetString());
        Assert.Equal("Timer", newValues["mode"].GetString());
        Assert.Equal("2026-07-07T09:30:00.0000000Z", newValues["startedAtUtc"].GetString());
        Assert.Equal(900, newValues["durationSeconds"].GetInt32());
    }

    [Fact]
    public void BuildForDelete_HasFullOldSnapshot_AndNullNewSide()
    {
        var diff = AuditDiffBuilder.BuildForDelete("Tag",
        [
            Unchanged("Name", "billing"),
            Unchanged("Color", "#ff0000"),
        ]);

        Assert.Null(diff.NewValuesJson);
        var oldValues = Parse(diff.OldValuesJson);
        Assert.Equal("billing", oldValues["name"].GetString());
        Assert.Equal("#ff0000", oldValues["color"].GetString());
    }

    [Fact]
    public void BuildForUpdate_IncludesOnlyChangedProperties()
    {
        var diff = AuditDiffBuilder.BuildForUpdate("TimeEntry",
        [
            Unchanged("Description", "standup"),
            Changed("DurationSeconds", 900, 1800),
            Changed("IsBillable", true, false),
        ]);

        Assert.NotNull(diff);
        var oldValues = Parse(diff!.OldValuesJson);
        var newValues = Parse(diff.NewValuesJson);

        Assert.Equal(2, oldValues.Count);
        Assert.Equal(900, oldValues["durationSeconds"].GetInt32());
        Assert.Equal(1800, newValues["durationSeconds"].GetInt32());
        Assert.True(oldValues["isBillable"].GetBoolean());
        Assert.False(newValues["isBillable"].GetBoolean());
        Assert.False(oldValues.ContainsKey("description"));
    }

    [Fact]
    public void BuildForUpdate_SkipsNoOpModifiedProperties()
    {
        var diff = AuditDiffBuilder.BuildForUpdate("TimeEntry",
        [
            Changed("Description", "standup", "standup"),
            Changed("DurationSeconds", 900, 1800),
        ]);

        Assert.NotNull(diff);
        var newValues = Parse(diff!.NewValuesJson);
        Assert.Single(newValues);
        Assert.True(newValues.ContainsKey("durationSeconds"));
    }

    [Fact]
    public void BuildForUpdate_ReturnsNull_WhenOnlyTimestampsChanged()
    {
        var now = DateTime.UtcNow;

        var diff = AuditDiffBuilder.BuildForUpdate("TimeEntry",
        [
            Changed("UpdatedAtUtc", now.AddMinutes(-5), now),
            Unchanged("Description", "standup"),
        ]);

        Assert.Null(diff);
    }

    [Fact]
    public void SensitiveProperties_AreMasked_OnBothSides()
    {
        var diff = AuditDiffBuilder.BuildForUpdate("Invitation",
        [
            Changed("TokenHash", "old-hash", "new-hash"),
            Changed("Status", InvitationStatus.Pending, InvitationStatus.Revoked),
        ]);

        Assert.NotNull(diff);
        var oldValues = Parse(diff!.OldValuesJson);
        var newValues = Parse(diff.NewValuesJson);

        Assert.Equal(AuditRedaction.Mask, oldValues["tokenHash"].GetString());
        Assert.Equal(AuditRedaction.Mask, newValues["tokenHash"].GetString());
        Assert.Equal("Pending", oldValues["status"].GetString());
        Assert.Equal("Revoked", newValues["status"].GetString());
    }

    [Fact]
    public void GoogleSub_IsMasked_OnCreate()
    {
        var diff = AuditDiffBuilder.BuildForCreate("User",
        [
            new AuditPropertySnapshot("Email", null, "alice@example.com", false),
            new AuditPropertySnapshot("GoogleSub", null, "google-oauth-subject", false),
        ]);

        var newValues = Parse(diff.NewValuesJson);
        Assert.Equal("alice@example.com", newValues["email"].GetString());
        Assert.Equal(AuditRedaction.Mask, newValues["googleSub"].GetString());
    }

    [Fact]
    public void BuildForSoftDelete_KeepsFullOldSnapshot_AndOnlyDeleteMarkersOnNewSide()
    {
        var deletedAt = DateTime.UtcNow;
        var deletedBy = Guid.NewGuid();

        var diff = AuditDiffBuilder.BuildForSoftDelete("TimeEntry",
        [
            Unchanged("Description", "standup"),
            Unchanged("DurationSeconds", 900),
            Changed("DeletedAtUtc", null, deletedAt),
            Changed("DeletedByUserId", null, deletedBy),
            Changed("UpdatedAtUtc", deletedAt.AddDays(-1), deletedAt),
        ]);

        var oldValues = Parse(diff.OldValuesJson);
        var newValues = Parse(diff.NewValuesJson);

        Assert.Equal("standup", oldValues["description"].GetString());
        Assert.Equal(900, oldValues["durationSeconds"].GetInt32());

        Assert.Equal(2, newValues.Count);
        Assert.Equal(deletedBy.ToString(), newValues["deletedByUserId"].GetString());
        Assert.True(newValues.ContainsKey("deletedAtUtc"));
    }

    [Fact]
    public void BuildForRestore_HasFullNewSnapshot_AndOnlyRestoreMarkersOnOldSide()
    {
        var deletedAt = DateTime.UtcNow.AddDays(-1);

        var diff = AuditDiffBuilder.BuildForRestore("TimeEntry",
        [
            Unchanged("Description", "standup"),
            Changed("DeletedAtUtc", deletedAt, null),
            Changed("DeletedByUserId", Guid.NewGuid(), null),
        ]);

        var oldValues = Parse(diff.OldValuesJson);
        var newValues = Parse(diff.NewValuesJson);

        Assert.Equal(2, oldValues.Count);
        Assert.True(oldValues.ContainsKey("deletedAtUtc"));
        Assert.Equal("standup", newValues["description"].GetString());
        Assert.Equal(JsonValueKind.Null, newValues["deletedAtUtc"].ValueKind);
    }
}
