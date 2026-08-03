using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ReeTrack.Api.Contracts;
using ReeTrack.Application.Common.Interfaces;
using ReeTrack.Application.Common.Models;

namespace ReeTrack.Api.Controllers;

[ApiController]
[Route("api/assistant")]
[Authorize]
public class AssistantController : ControllerBase
{
    private readonly IAssistantService _assistantService;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public AssistantController(IAssistantService assistantService)
    {
        _assistantService = assistantService;
    }

    /// <summary>
    /// Streams an AI assistant conversation via Server-Sent Events.
    /// Named event types: token, tool_call, tool_result, draft, done, error.
    /// </summary>
    [HttpPost("chat")]
    public async Task Chat(
        [FromBody] Contracts.AssistantChatRequest request,
        CancellationToken cancellationToken)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");

        var domainRequest = new Application.Common.Models.AssistantChatRequest
        {
            ConversationId = request.ConversationId,
            Message = request.Message,
            History = request.History.Select(h => new AssistantMessage
            {
                Role = h.Role,
                Content = h.Content,
            }).ToList(),
            CurrentDraft = request.CurrentDraft?.ToDomain(),
            Mentions = request.Mentions?
                .Select(m => new MessageMention(m.Type, m.Id, m.Name))
                .ToList(),
        };

        await foreach (var evt in _assistantService.StreamChatAsync(domainRequest, cancellationToken))
        {
            var (eventName, json) = evt switch
            {
                AssistantEvent.TokenEvent token => ("token", JsonSerializer.Serialize(new { text = token.Text }, JsonOptions)),
                AssistantEvent.DraftEvent draft => ("draft", JsonSerializer.Serialize(new { draft = ProjectDraftDto.FromDomain(draft.Draft) }, JsonOptions)),
                AssistantEvent.DoneEvent done => ("done", JsonSerializer.Serialize(new { conversationId = done.ConversationId, draftCleared = done.DraftCleared }, JsonOptions)),
                AssistantEvent.ErrorEvent error => ("error", JsonSerializer.Serialize(new { message = error.Message }, JsonOptions)),
                _ => ("unknown", "{}"),
            };

            var sseMessage = $"event: {eventName}\ndata: {json}\n\n";
            await Response.WriteAsync(sseMessage, cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }
}
