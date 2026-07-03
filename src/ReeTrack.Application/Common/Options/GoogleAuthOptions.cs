namespace ReeTrack.Application.Common.Options;

public class GoogleAuthOptions
{
    public const string SectionName = "Google";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Optional. When set, only this email may become the first admin on initial setup.
    /// </summary>
    public string? AdminEmail { get; set; }
}
