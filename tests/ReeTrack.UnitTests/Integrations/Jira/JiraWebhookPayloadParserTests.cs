using System.Text;
using ReeTrack.Application.Integrations.Jira;
using Xunit;

namespace ReeTrack.UnitTests.Integrations.Jira;

public class JiraWebhookPayloadParserTests
{
    [Fact]
    public void TryParse_MapsIssueUpdatedPayload()
    {
        var json = """
            {
              "webhookEvent": "jira:issue_updated",
              "issue": {
                "id": "10001",
                "key": "DEMO-1",
                "fields": {
                  "summary": "Rename me",
                  "project": { "id": "20001", "key": "DEMO" },
                  "status": { "statusCategory": { "key": "done" } },
                  "assignee": { "emailAddress": "dev@example.com" },
                  "timetracking": { "originalEstimateSeconds": 7200 },
                  "issuetype": { "subtask": false }
                }
              }
            }
            """;

        var ok = JiraWebhookPayloadParser.TryParse(Encoding.UTF8.GetBytes(json), out var parsed);

        Assert.True(ok);
        Assert.NotNull(parsed);
        Assert.Equal("jira:issue_updated", parsed!.WebhookEvent);
        Assert.Equal("20001", parsed.JiraProjectId);
        Assert.Equal("DEMO", parsed.JiraProjectKey);
        Assert.False(parsed.IsSubtask);
        Assert.Equal("10001", parsed.Issue.Id);
        Assert.Equal("DEMO-1", parsed.Issue.Key);
        Assert.Equal("Rename me", parsed.Issue.Summary);
        Assert.True(parsed.Issue.IsDone);
        Assert.Equal("dev@example.com", parsed.Issue.AssigneeEmail);
        Assert.Equal(2m, parsed.Issue.OriginalEstimateHours);
    }

    [Fact]
    public void TryParse_RejectsUnsupportedEventAndSubtasksMissingProject()
    {
        Assert.False(JiraWebhookPayloadParser.TryParse(
            Encoding.UTF8.GetBytes("""{"webhookEvent":"jira:issue_deleted","issue":{"id":"1","key":"A-1","fields":{"project":{"id":"1","key":"A"}}}}"""),
            out _));

        Assert.True(JiraWebhookPayloadParser.TryParse(
            Encoding.UTF8.GetBytes("""
                {
                  "webhookEvent": "jira:issue_created",
                  "issue": {
                    "id": "1",
                    "key": "A-1",
                    "fields": {
                      "summary": "Sub",
                      "project": { "id": "1", "key": "A" },
                      "issuetype": { "subtask": true }
                    }
                  }
                }
                """),
            out var subtask));
        Assert.True(subtask!.IsSubtask);

        Assert.False(JiraWebhookPayloadParser.TryParse(
            Encoding.UTF8.GetBytes("""{"webhookEvent":"jira:issue_updated","issue":{"id":"1","key":"A-1","fields":{}}}"""),
            out _));
    }
}
