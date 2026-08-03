using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;
using ReeTrack.Infrastructure.Assistant;
using Xunit;

namespace ReeTrack.UnitTests.Assistant;

public class AssistantServiceTests
{
    private readonly FakeClientService _clientService = new();
    private readonly FakeProjectService _projectService = new();

    [Fact]
    public async Task StreamChat_ReturnsTokenEvent_WithPlainTextResponse()
    {
        var fakeClient = new FakeChatClient(["Hello! How can I help?"]);
        var sut = CreateSut(fakeClient);

        var events = await CollectEventsAsync(sut, "hi");

        var token = Assert.IsType<AssistantEvent.TokenEvent>(events[0]);
        Assert.Equal("Hello! How can I help?", token.Text);
        Assert.IsType<AssistantEvent.DoneEvent>(events[1]);
    }

    [Fact]
    public async Task StreamChat_ReturnsDraftEvent_WhenSubmitDraftToolCalled()
    {
        var clientId = Guid.NewGuid();
        _clientService.SearchResults["acme"] = [new ClientLookupDto(clientId, "Acme Corp")];

        var fakeClient = new FakeChatClient(["Draft submitted for Acme Corp."]);
        var sut = CreateSut(fakeClient);

        var events = await CollectEventsAsync(sut, "Create a website project for Acme Corp");

        Assert.Contains(events, e => e is AssistantEvent.TokenEvent);
        Assert.Contains(events, e => e is AssistantEvent.DoneEvent);
    }

    [Fact]
    public async Task StreamChat_ClearsDraft_WhenNoDraft()
    {
        var fakeClient = new FakeChatClient(["No problem, I've cleared the draft."]);
        var sut = CreateSut(fakeClient);

        var events = await CollectEventsAsync(sut, "never mind");

        var done = Assert.IsType<AssistantEvent.DoneEvent>(events[^1]);
        Assert.False(done.DraftCleared);
    }

    [Fact]
    public async Task StreamChat_IncludesCurrentDraft_WhenRefining()
    {
        var existingDraft = new ProjectDraft
        {
            Name = "Existing Project",
            ClientId = Guid.NewGuid(),
            ClientName = "Test Client",
            CurrencyCode = "EUR",
            Tasks = [new ProjectTaskDraft { Name = "Task 1", TimeEstimateHours = 5 }]
        };

        var fakeClient = new FakeChatClient(["I've updated the draft."]);
        var sut = CreateSut(fakeClient);

        var request = new AssistantChatRequest
        {
            Message = "Add another task",
            CurrentDraft = existingDraft,
            History = []
        };

        var events = new List<AssistantEvent>();
        await foreach (var evt in sut.StreamChatAsync(request))
            events.Add(evt);

        Assert.Contains(events, e => e is AssistantEvent.TokenEvent);

        var userMessages = fakeClient.ReceivedMessages
            .Where(m => m.Role == ChatRole.User)
            .ToList();

        Assert.Contains(userMessages, m =>
            m.Text != null
            && m.Text.Contains("Add another task")
            && m.Text.Contains("<current_project_draft>")
            && m.Text.Contains("Existing Project")
            && m.Text.Contains("</current_project_draft>"));

        // Draft JSON must live in the user turn, not as a mid-thread system payload.
        Assert.DoesNotContain(
            fakeClient.ReceivedMessages.Where(m => m.Role == ChatRole.System),
            m => m.Text != null && m.Text.Contains("Existing Project"));
    }

    [Fact]
    public async Task StreamChat_EmbedsCurrentDraft_InLatestUserMessage_AfterHistory()
    {
        var existingDraft = new ProjectDraft
        {
            Name = "Edited In UI",
            ClientId = Guid.NewGuid(),
            ClientName = "Test Client",
            CurrencyCode = "EUR",
            HourlyRate = 120m,
            Tasks = [new ProjectTaskDraft { Name = "Task 1", TimeEstimateHours = 5 }]
        };

        var fakeClient = new FakeChatClient(["I've updated the draft."]);
        var sut = CreateSut(fakeClient);

        var request = new AssistantChatRequest
        {
            Message = "Set estimate to 40h",
            CurrentDraft = existingDraft,
            History =
            [
                new AssistantMessage { Role = "user", Content = "Create a project" },
                new AssistantMessage { Role = "assistant", Content = "Draft proposed with hourly rate 90." },
            ]
        };

        await foreach (var _ in sut.StreamChatAsync(request)) { }

        var historyAssistantIndex = fakeClient.ReceivedMessages.FindIndex(m =>
            m.Role == ChatRole.Assistant && m.Text != null && m.Text.Contains("hourly rate 90"));
        var latestUserIndex = fakeClient.ReceivedMessages.FindIndex(m =>
            m.Role == ChatRole.User
            && m.Text != null
            && m.Text.Contains("Set estimate to 40h")
            && m.Text.Contains("Edited In UI")
            && m.Text.Contains("120"));

        Assert.True(historyAssistantIndex >= 0);
        Assert.True(latestUserIndex > historyAssistantIndex);

        Assert.DoesNotContain(
            fakeClient.ReceivedMessages.Where(m => m.Role == ChatRole.System),
            m => m.Text != null && m.Text.Contains("Edited In UI"));
    }

    [Fact]
    public async Task StreamChat_SetsCorrectConversationId()
    {
        var fakeClient = new FakeChatClient(["OK"]);
        var sut = CreateSut(fakeClient);

        var events = await CollectEventsAsync(sut, "hi", conversationId: "test-conv-123");

        var done = Assert.IsType<AssistantEvent.DoneEvent>(events[^1]);
        Assert.Equal("test-conv-123", done.ConversationId);
    }

    [Fact]
    public async Task StreamChat_GeneratesConversationId_WhenNotProvided()
    {
        var fakeClient = new FakeChatClient(["OK"]);
        var sut = CreateSut(fakeClient);

        var events = await CollectEventsAsync(sut, "hi");

        var done = Assert.IsType<AssistantEvent.DoneEvent>(events[^1]);
        Assert.False(string.IsNullOrWhiteSpace(done.ConversationId));
    }

    [Fact]
    public async Task StreamChat_SendsSystemPrompt()
    {
        var fakeClient = new FakeChatClient(["OK"]);
        var sut = CreateSut(fakeClient);

        await CollectEventsAsync(sut, "hi");

        var systemMessages = fakeClient.ReceivedMessages
            .Where(m => m.Role == ChatRole.System)
            .ToList();

        Assert.Contains(systemMessages, m =>
            m.Text != null && m.Text.Contains("ReeTrack"));
        Assert.Contains(systemMessages, m =>
            m.Text != null && m.Text.Contains("SearchClients"));
    }

    [Fact]
    public async Task StreamChat_SendsTools()
    {
        var fakeClient = new FakeChatClient(["OK"]);
        var sut = CreateSut(fakeClient);

        await CollectEventsAsync(sut, "hi");

        Assert.NotNull(fakeClient.ReceivedOptions);
        Assert.NotNull(fakeClient.ReceivedOptions!.Tools);
        Assert.Equal(4, fakeClient.ReceivedOptions!.Tools.Count);
    }

    [Fact]
    public async Task StreamChat_HistoryMessagesAreIncluded()
    {
        var fakeClient = new FakeChatClient(["OK"]);
        var sut = CreateSut(fakeClient);

        var request = new AssistantChatRequest
        {
            Message = "What about now?",
            History =
            [
                new AssistantMessage { Role = "user", Content = "Create a project" },
                new AssistantMessage { Role = "assistant", Content = "Sure, what's the name?" },
            ]
        };

        var events = new List<AssistantEvent>();
        await foreach (var evt in sut.StreamChatAsync(request))
            events.Add(evt);

        var userMessages = fakeClient.ReceivedMessages
            .Where(m => m.Role == ChatRole.User)
            .ToList();

        Assert.True(userMessages.Count >= 2);
        Assert.Contains(userMessages, m => m.Text == "Create a project");
        Assert.Contains(userMessages, m => m.Text == "What about now?");
    }

    #region AssistantTools Tests

    [Fact]
    public async Task AssistantTools_SearchClients_ReturnsFormattedResults()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        _clientService.SearchResults["test"] = [new ClientLookupDto(id1, "Test A"), new ClientLookupDto(id2, "Test B")];

        var tools = CreateTools();
        var result = await tools.SearchClients("test");

        Assert.Contains("Test A", result);
        Assert.Contains("Test B", result);
        Assert.Contains(id1.ToString(), result);
        Assert.Contains(id2.ToString(), result);
    }

    [Fact]
    public async Task AssistantTools_SearchClients_NoResults()
    {
        var tools = CreateTools();
        var result = await tools.SearchClients("zzz");

        Assert.Equal("No matching clients found.", result);
    }

    [Fact]
    public async Task AssistantTools_SearchProjects_ReturnsFormattedResults()
    {
        var id = Guid.NewGuid();
        _projectService.SearchResults["web"] = [new ProjectLookupDto(id, "Website Project", "Acme Corp", 5)];

        var tools = CreateTools();
        var result = await tools.SearchProjects("web");

        Assert.Contains("Website Project", result);
        Assert.Contains("Acme Corp", result);
        Assert.Contains("5", result);
    }

    [Fact]
    public async Task AssistantTools_SearchProjects_NoResults()
    {
        var tools = CreateTools();
        var result = await tools.SearchProjects("zzz");

        Assert.Equal("No matching projects found.", result);
    }

    [Fact]
    public void AssistantTools_SubmitDraft_StoresDraftCorrectly()
    {
        var clientId = Guid.NewGuid();
        var tools = CreateTools();

        tools.SubmitDraft(
            name: "Website Redesign",
            clientId: clientId,
            clientName: "Acme Corp",
            currencyCode: "USD",
            hourlyRate: 100m,
            fixedFeeAmount: 5000m,
            timeEstimateHours: 50m,
            color: "#FF0000",
            tasks:
            [
                new ProjectTaskDraft { Name = "Design", TimeEstimateHours = 20 },
                new ProjectTaskDraft { Name = "Development", TimeEstimateHours = 30 },
            ]);

        Assert.NotNull(tools.CapturedDraft);
        Assert.Equal("Website Redesign", tools.CapturedDraft.Name);
        Assert.Equal(clientId, tools.CapturedDraft.ClientId);
        Assert.Equal("Acme Corp", tools.CapturedDraft.ClientName);
        Assert.Equal("USD", tools.CapturedDraft.CurrencyCode);
        Assert.Equal(100m, tools.CapturedDraft.HourlyRate);
        Assert.Equal(5000m, tools.CapturedDraft.FixedFeeAmount);
        Assert.Equal(50m, tools.CapturedDraft.TimeEstimateHours);
        Assert.Equal("#FF0000", tools.CapturedDraft.Color);
        Assert.Equal(2, tools.CapturedDraft.Tasks.Count);
    }

    [Fact]
    public void AssistantTools_SubmitDraft_RejectsMissingClientId()
    {
        var tools = CreateTools();

        var result = tools.SubmitDraft(name: "Website Redesign", clientId: null, clientName: "Acme");

        Assert.Contains("valid clientId is required", result);
        Assert.Null(tools.CapturedDraft);
    }

    [Fact]
    public void AssistantTools_SubmitDraft_OverlaysOntoSeededBaseDraft()
    {
        var clientId = Guid.NewGuid();
        var tools = CreateTools();
        tools.SeedBaseDraft(new ProjectDraft
        {
            Name = "UI Edited Name",
            ClientId = clientId,
            ClientName = "Acme Corp",
            CurrencyCode = "USD",
            HourlyRate = 120m,
            FixedFeeAmount = 5000m,
            TimeEstimateHours = 40m,
            Color = "#112233",
            Tasks =
            [
                new ProjectTaskDraft { Name = "Design", TimeEstimateHours = 10 },
                new ProjectTaskDraft { Name = "Build", TimeEstimateHours = 30 },
            ]
        });

        // Model only changes estimate; omitted fields must keep UI values.
        tools.SubmitDraft(timeEstimateHours: 55m);

        Assert.NotNull(tools.CapturedDraft);
        Assert.Equal("UI Edited Name", tools.CapturedDraft.Name);
        Assert.Equal(clientId, tools.CapturedDraft.ClientId);
        Assert.Equal("Acme Corp", tools.CapturedDraft.ClientName);
        Assert.Equal("USD", tools.CapturedDraft.CurrencyCode);
        Assert.Equal(120m, tools.CapturedDraft.HourlyRate);
        Assert.Equal(5000m, tools.CapturedDraft.FixedFeeAmount);
        Assert.Equal(55m, tools.CapturedDraft.TimeEstimateHours);
        Assert.Equal("#112233", tools.CapturedDraft.Color);
        Assert.Equal(2, tools.CapturedDraft.Tasks.Count);
        Assert.Equal("Design", tools.CapturedDraft.Tasks[0].Name);
    }

    [Fact]
    public void AssistantTools_Reset_ClearsSeededBaseDraft()
    {
        var tools = CreateTools();
        tools.SeedBaseDraft(new ProjectDraft
        {
            Name = "Seeded",
            ClientId = Guid.NewGuid(),
            ClientName = "Client",
            CurrencyCode = "EUR",
        });

        tools.Reset();

        var result = tools.SubmitDraft(name: "Only Name");
        Assert.Contains("valid clientId is required", result);
        Assert.Null(tools.CapturedDraft);
    }

    [Fact]
    public void AssistantTools_ClearDraft_ResetsState()
    {
        var tools = CreateTools();

        tools.SubmitDraft("Test", Guid.NewGuid(), "Client", "EUR");
        Assert.NotNull(tools.CapturedDraft);

        tools.ClearDraft();
        Assert.Null(tools.CapturedDraft);
        Assert.True(tools.DraftCleared);
    }

    [Fact]
    public void AssistantTools_Reset_ClearsAllState()
    {
        var tools = CreateTools();

        tools.SubmitDraft("Test", Guid.NewGuid(), "Client", "EUR");
        tools.ClearDraft();

        tools.Reset();

        Assert.Null(tools.CapturedDraft);
        Assert.False(tools.DraftCleared);
    }

    [Fact]
    public async Task AssistantTools_SearchClients_CallsServiceWithCorrectQuery()
    {
        var tools = CreateTools();

        await tools.SearchClients("Acme");

        Assert.Single(_clientService.SearchCalls);
        Assert.Equal("Acme", _clientService.SearchCalls[0]);
    }

    [Fact]
    public async Task AssistantTools_SearchProjects_CallsServiceWithCorrectQuery()
    {
        var tools = CreateTools();

        await tools.SearchProjects("website");

        Assert.Single(_projectService.SearchCalls);
        Assert.Equal("website", _projectService.SearchCalls[0]);
    }

    #endregion

    #region Helpers

    private AssistantService CreateSut(FakeChatClient fakeClient)
    {
        var tools = new AssistantTools(_clientService, _projectService);
        var logger = NullLogger<AssistantService>.Instance;
        return new AssistantService(fakeClient, tools, logger);
    }

    private AssistantTools CreateTools() => new(_clientService, _projectService);

    private static async Task<List<AssistantEvent>> CollectEventsAsync(
        AssistantService sut,
        string message,
        string? conversationId = null)
    {
        var request = new AssistantChatRequest
        {
            ConversationId = conversationId,
            Message = message,
            History = []
        };

        var events = new List<AssistantEvent>();
        await foreach (var evt in sut.StreamChatAsync(request))
            events.Add(evt);

        return events;
    }

    #endregion
}

#region Fakes

public sealed class FakeChatClient : IChatClient
{
    private readonly Queue<string> _responses;

    public List<ChatMessage> ReceivedMessages { get; } = [];
    public ChatOptions? ReceivedOptions { get; private set; }

    public FakeChatClient(IReadOnlyList<string> responseTexts)
    {
        _responses = new Queue<string>(responseTexts);
    }

    public ChatClientMetadata Metadata => new("fake", new Uri("https://fake.test"));

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ReceivedMessages.Clear();
        ReceivedMessages.AddRange(messages);
        ReceivedOptions = options;

        var text = _responses.Count > 0 ? _responses.Dequeue() : "OK";
        var response = new ChatResponse
        {
            Messages = [new ChatMessage(ChatRole.Assistant, text)]
        };

        return Task.FromResult(response);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ReceivedMessages.Clear();
        ReceivedMessages.AddRange(messages);
        ReceivedOptions = options;

        var text = _responses.Count > 0 ? _responses.Dequeue() : "OK";

        var chunkSize = 5;
        for (var i = 0; i < text.Length; i += chunkSize)
        {
            var chunk = text.Substring(i, Math.Min(chunkSize, text.Length - i));
            yield return new ChatResponseUpdate
            {
                Role = ChatRole.Assistant,
                Contents = [new TextContent(chunk)],
            };
        }
    }

    public object? GetService(Type serviceType, object? key = null) => null;

    public void Dispose() { }
}

public sealed class FakeClientService : IClientService
{
    public Dictionary<string, List<ClientLookupDto>> SearchResults { get; } = [];
    public List<string> SearchCalls { get; } = [];

    public Task<IReadOnlyList<ClientLookupDto>> SearchAsync(
        string query, int maxResults = 10, CancellationToken cancellationToken = default)
    {
        SearchCalls.Add(query);
        var key = query.ToLowerInvariant();
        var results = SearchResults.TryGetValue(key, out var found) ? found : [];
        return Task.FromResult<IReadOnlyList<ClientLookupDto>>(results);
    }

    public Task<PagedResult<ClientDto>> ListAsync(ClientListQuery query, CancellationToken cancellationToken = default)
        => Task.FromResult(new PagedResult<ClientDto> { Items = [], TotalCount = 0, Page = 1, PageSize = 20 });

    public Task<ClientDto> CreateAsync(string? name, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<ClientDto> UpdateAsync(Guid id, string? name, bool? isActive, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}

public sealed class FakeProjectService : IProjectService
{
    public Dictionary<string, List<ProjectLookupDto>> SearchResults { get; } = [];
    public List<string> SearchCalls { get; } = [];

    public Task<IReadOnlyList<ProjectLookupDto>> SearchAsync(
        string query, int maxResults = 10, CancellationToken cancellationToken = default)
    {
        SearchCalls.Add(query);
        var key = query.ToLowerInvariant();
        var results = SearchResults.TryGetValue(key, out var found) ? found : [];
        return Task.FromResult<IReadOnlyList<ProjectLookupDto>>(results);
    }

    public Task<PagedResult<ProjectDto>> ListAsync(ProjectListQuery query, CancellationToken cancellationToken = default)
        => Task.FromResult(new PagedResult<ProjectDto> { Items = [], TotalCount = 0, Page = 1, PageSize = 20 });

    public Task<ProjectDto> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<ProjectDto> CreateAsync(CreateProjectInput input, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<ProjectDto> CreateWithTasksAsync(CreateProjectWithTasksInput input, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectInput input, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}

#endregion
