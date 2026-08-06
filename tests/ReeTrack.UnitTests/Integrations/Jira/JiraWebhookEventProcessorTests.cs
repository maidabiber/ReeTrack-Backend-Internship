using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Options;
using ReeTrack.Application.Integrations.Jira;
using ReeTrack.Domain.Entities;
using ReeTrack.Domain.Enums;
using ReeTrack.Infrastructure.Integrations.Jira;
using ReeTrack.Infrastructure.Persistence;
using Xunit;

namespace ReeTrack.UnitTests.Integrations.Jira;

public class JiraWebhookEventProcessorTests
{
    [Fact]
    public async Task ProcessAsync_UpsertsTaskForIntegratedProject()
    {
        await using var db = CreateDbContext();
        var projectId = await SeedIntegratedProjectAsync(db, externalId: "20001", externalKey: "DEMO");
        var processor = CreateProcessor(db);

        var payload = Encoding.UTF8.GetBytes("""
            {
              "webhookEvent": "jira:issue_updated",
              "issue": {
                "id": "10001",
                "key": "DEMO-1",
                "fields": {
                  "summary": "From webhook",
                  "project": { "id": "20001", "key": "DEMO" },
                  "status": { "statusCategory": { "key": "indeterminate" } },
                  "issuetype": { "subtask": false }
                }
              }
            }
            """);

        await processor.ProcessAsync(payload);

        var task = Assert.Single(db.ProjectTasks.Where(t => t.ProjectId == projectId));
        Assert.Equal("10001", task.ExternalId);
        Assert.Equal("DEMO-1: From webhook", task.Name);
        Assert.Equal(ProjectTaskStatus.Open, task.Status);

        var updated = Encoding.UTF8.GetBytes("""
            {
              "webhookEvent": "jira:issue_updated",
              "issue": {
                "id": "10001",
                "key": "DEMO-1",
                "fields": {
                  "summary": "Updated via webhook",
                  "project": { "id": "20001", "key": "DEMO" },
                  "status": { "statusCategory": { "key": "done" } },
                  "issuetype": { "subtask": false }
                }
              }
            }
            """);

        await processor.ProcessAsync(updated);

        Assert.Equal(1, await db.ProjectTasks.CountAsync(t => t.ProjectId == projectId));
        task = Assert.Single(db.ProjectTasks.Where(t => t.ProjectId == projectId));
        Assert.Equal("DEMO-1: Updated via webhook", task.Name);
        Assert.Equal(ProjectTaskStatus.Done, task.Status);
    }

    [Fact]
    public async Task ProcessAsync_NoOpsForUnintegratedProjectInactiveAndSubtask()
    {
        await using var db = CreateDbContext();
        db.JiraWebhookSettings.Add(new JiraWebhookSettings
        {
            Id = Guid.NewGuid(),
            SingletonKey = 1,
            IsActive = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var inactiveProcessor = CreateProcessor(db);
        var inactivePayload = Encoding.UTF8.GetBytes("""
            {
              "webhookEvent": "jira:issue_created",
              "issue": {
                "id": "9",
                "key": "X-9",
                "fields": {
                  "summary": "Should skip",
                  "project": { "id": "999", "key": "X" },
                  "issuetype": { "subtask": false }
                }
              }
            }
            """);
        await inactiveProcessor.ProcessAsync(inactivePayload);
        Assert.Empty(db.ProjectTasks);

        db.JiraWebhookSettings.Single().IsActive = true;
        await db.SaveChangesAsync();

        var activeProcessor = CreateProcessor(db);
        await activeProcessor.ProcessAsync(inactivePayload);
        Assert.Empty(db.ProjectTasks);

        var subtaskPayload = Encoding.UTF8.GetBytes("""
            {
              "webhookEvent": "jira:issue_created",
              "issue": {
                "id": "10",
                "key": "X-10",
                "fields": {
                  "summary": "Subtask",
                  "project": { "id": "999", "key": "X" },
                  "issuetype": { "subtask": true }
                }
              }
            }
            """);
        await activeProcessor.ProcessAsync(subtaskPayload);
        Assert.Empty(db.ProjectTasks);
    }

    private static JiraWebhookEventProcessor CreateProcessor(AppDbContext db)
    {
        var options = Options.Create(new JiraOptions
        {
            SiteUrl = "https://example.atlassian.net",
            Email = "user@example.com",
            ApiToken = "token",
            WebhookSecret = "test-secret"
        });

        var webhooks = new JiraWebhookSubscriptionService(db, options);
        var jira = new JiraIntegrationService(db, new StubJiraApiClient(), options);
        return new JiraWebhookEventProcessor(webhooks, jira, NullLogger<JiraWebhookEventProcessor>.Instance);
    }

    private static async Task<Guid> SeedIntegratedProjectAsync(
        AppDbContext db,
        string externalId,
        string externalKey)
    {
        var client = new Client
        {
            Id = Guid.NewGuid(),
            Name = "Acme",
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        var project = new Project
        {
            Id = Guid.NewGuid(),
            ClientId = client.Id,
            Name = "Demo",
            Status = ProjectStatus.Active,
            CreatedByUserId = Guid.NewGuid(),
            CurrencyCode = "EUR",
            ExternalProvider = ExternalProvider.Jira,
            ExternalId = externalId,
            ExternalKey = externalKey,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };
        db.Clients.Add(client);
        db.Projects.Add(project);
        await db.SaveChangesAsync();
        return project.Id;
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private sealed class StubJiraApiClient : IJiraApiClient
    {
        public Task<IReadOnlyList<JiraApiProject>> ListProjectsAsync(
            string siteUrl,
            string email,
            string apiToken,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<JiraApiProject>>([]);

        public Task<IReadOnlyList<JiraApiIssue>> ListIssuesAsync(
            string siteUrl,
            string email,
            string apiToken,
            string projectKey,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<JiraApiIssue>>([]);
    }
}
