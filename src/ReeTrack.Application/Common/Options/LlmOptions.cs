namespace ReeTrack.Application.Common.Options;

/// <summary>
/// OpenAI-compatible LLM settings. Defaults target Groq's free tier.
/// </summary>
public class LlmOptions
{
    public const string SectionName = "Llm";

    /// <summary>
    /// Groq API key from https://console.groq.com/keys.
    /// Prefer User Secrets / env: Llm__ApiKey (or GROQ_API_KEY via .env mapping).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Groq model id. Prefer models with strict Structured Outputs support,
    /// e.g. openai/gpt-oss-20b or openai/gpt-oss-120b.
    /// </summary>
    public string Model { get; set; } = "openai/gpt-oss-20b";

    /// <summary>Groq OpenAI-compatible base URL.</summary>
    public string BaseUrl { get; set; } = "https://api.groq.com/openai/v1";
}
