namespace ReeTrack.IntegrationTests.Support;

internal static class FakeEmailSenderExtensions
{
    public static async Task WaitForMentionEmailAsync(
        this FakeEmailSender sender,
        string expectedEmail,
        int timeoutMs = 2000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTime.UtcNow < deadline)
        {
            if (sender.LastMentionToEmail == expectedEmail)
                return;

            await Task.Delay(10);
        }

        Xunit.Assert.Equal(expectedEmail, sender.LastMentionToEmail);
    }
}
