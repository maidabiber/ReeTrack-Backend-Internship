namespace ReeTrack.Application.Common.Options;

public class ProjectThresholdOptions
{
    public const string SectionName = "ProjectThreshold";

    /// <summary>UTC time of day when evaluation runs (HH:mm). Default 02:00.</summary>
    public string EvaluationTimeUtc { get; set; } = "02:00";

    /// <summary>UTC time of day when pending alerts are delivered (HH:mm). Default 08:00.</summary>
    public string DeliveryTimeUtc { get; set; } = "08:00";

    public int PollIntervalMinutes { get; set; } = 15;
}
