namespace ReeTrack.IntegrationTests.Support;

internal static class FakeEmailChannelProviderExtensions
{
    public static async Task WaitForMentionEmailAsync(
        this FakeEmailChannelProvider channel,
        Guid expectedUserId,
        int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            if (channel.LastMentionUserId == expectedUserId)
                return;

            await Task.Delay(10);
        }

        Xunit.Assert.Equal(expectedUserId, channel.LastMentionUserId);
    }

    public static async Task WaitForDecisionEmailCountAsync(
        this FakeEmailChannelProvider channel,
        int expectedCount,
        int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            if (channel.DecisionEmails.Count >= expectedCount)
                return;

            await Task.Delay(10);
        }

        Xunit.Assert.True(
            channel.DecisionEmails.Count >= expectedCount,
            $"Expected at least {expectedCount} decision emails, got {channel.DecisionEmails.Count}.");
    }
}
