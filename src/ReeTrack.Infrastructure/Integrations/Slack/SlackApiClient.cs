using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ReeTrack.Application.Common.Options;
using ReeTrack.Application.Integrations.Slack;

namespace ReeTrack.Infrastructure.Integrations.Slack;

/// <summary>
/// Slack Web API client with a process-wide delay between outbound DMs.
/// </summary>
public sealed class SlackApiClient : ISlackApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly SemaphoreSlim SendGate = new(1, 1);
    private static DateTimeOffset _lastSendUtc = DateTimeOffset.MinValue;

    private readonly HttpClient _httpClient;
    private readonly SlackOptions _options;
    private readonly ILogger<SlackApiClient> _logger;

    public SlackApiClient(
        HttpClient httpClient,
        IOptions<SlackOptions> options,
        ILogger<SlackApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> LookupUserIdByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BotToken))
            return null;

        if (string.IsNullOrWhiteSpace(email))
            return null;

        using var request = CreateRequest(
            HttpMethod.Get,
            $"users.lookupByEmail?email={Uri.EscapeDataString(email.Trim())}");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var payload = await ReadSlackResponseAsync<LookupByEmailResponse>(response, cancellationToken);

        if (payload is null)
            return null;

        if (!payload.Ok)
        {
            if (!string.Equals(payload.Error, "users_not_found", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Slack users.lookupByEmail failed for {Email}: {Error}",
                    email,
                    payload.Error);
            }

            return null;
        }

        return string.IsNullOrWhiteSpace(payload.User?.Id) ? null : payload.User.Id;
    }

    public async Task SendDirectMessageAsync(
        string slackUserId,
        string text,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slackUserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (string.IsNullOrWhiteSpace(_options.BotToken))
        {
            _logger.LogDebug("Skipping Slack DM: BotToken is not configured.");
            return;
        }

        await SendGate.WaitAsync(cancellationToken);
        try
        {
            await WaitForSendDelayUnlockedAsync(cancellationToken);

            var channelId = await OpenDirectMessageAsync(slackUserId, cancellationToken);
            if (string.IsNullOrWhiteSpace(channelId))
                return;

            var body = JsonSerializer.Serialize(new { channel = channelId, text });
            using var request = CreateRequest(HttpMethod.Post, "chat.postMessage");
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var payload = await ReadSlackResponseAsync<SlackApiResponse>(response, cancellationToken);

            if (payload is null || !payload.Ok)
            {
                _logger.LogWarning(
                    "Slack chat.postMessage failed for user {SlackUserId}: {Error}",
                    slackUserId,
                    payload?.Error ?? "empty_response");
            }

            _lastSendUtc = DateTimeOffset.UtcNow;
        }
        finally
        {
            SendGate.Release();
        }
    }

    private async Task<string?> OpenDirectMessageAsync(
        string slackUserId,
        CancellationToken cancellationToken)
    {
        var body = JsonSerializer.Serialize(new { users = slackUserId });
        using var request = CreateRequest(HttpMethod.Post, "conversations.open");
        request.Content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var payload = await ReadSlackResponseAsync<ConversationsOpenResponse>(response, cancellationToken);

        if (payload is null || !payload.Ok || string.IsNullOrWhiteSpace(payload.Channel?.Id))
        {
            _logger.LogWarning(
                "Slack conversations.open failed for user {SlackUserId}: {Error}",
                slackUserId,
                payload?.Error ?? "empty_response");
            return null;
        }

        return payload.Channel.Id;
    }

    private async Task WaitForSendDelayUnlockedAsync(CancellationToken cancellationToken)
    {
        var delayMs = Math.Max(0, _options.SendDelayMilliseconds);
        if (delayMs <= 0 || _lastSendUtc == DateTimeOffset.MinValue)
            return;

        var elapsed = DateTimeOffset.UtcNow - _lastSendUtc;
        var remaining = TimeSpan.FromMilliseconds(delayMs) - elapsed;
        if (remaining > TimeSpan.Zero)
            await Task.Delay(remaining, cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl)
    {
        var request = new HttpRequestMessage(method, relativeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.BotToken);
        return request;
    }

    private static async Task<T?> ReadSlackResponseAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
        where T : class
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }

    private class SlackApiResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    private sealed class LookupByEmailResponse : SlackApiResponse
    {
        [JsonPropertyName("user")]
        public SlackUser? User { get; set; }
    }

    private sealed class ConversationsOpenResponse : SlackApiResponse
    {
        [JsonPropertyName("channel")]
        public SlackChannel? Channel { get; set; }
    }

    private sealed class SlackUser
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }

    private sealed class SlackChannel
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }
}
