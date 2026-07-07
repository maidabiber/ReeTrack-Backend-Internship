using ReeTrack.Application.Common.Invitations;
using Xunit;

namespace ReeTrack.UnitTests.Invitations;

public class InvitationTokenHelperTests
{
    [Fact]
    public void HashToken_IsDeterministic()
    {
        const string token = "sample-invite-token";

        var first = InvitationTokenHelper.HashToken(token);
        var second = InvitationTokenHelper.HashToken(token);

        Assert.Equal(first, second);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void GenerateToken_ProducesUniqueValues()
    {
        var first = InvitationTokenHelper.GenerateToken();
        var second = InvitationTokenHelper.GenerateToken();

        Assert.NotEqual(first, second);
        Assert.NotEmpty(first);
    }

    [Theory]
    [InlineData("  Alice@Example.COM ", "alice@example.com")]
    [InlineData("bob@test.co", "bob@test.co")]
    public void NormalizeEmail_TrimsAndLowercases(string input, string expected)
    {
        Assert.Equal(expected, InvitationTokenHelper.NormalizeEmail(input));
    }

    [Theory]
    [InlineData("alice.smith@example.com", "Alice Smith")]
    [InlineData("bob@example.com", "Bob")]
    public void DisplayNameFromEmail_FormatsLocalPart(string email, string expected)
    {
        Assert.Equal(expected, InvitationTokenHelper.DisplayNameFromEmail(email));
    }

    [Theory]
    [InlineData("alice@reeinvent.com", "reeinvent.com")]
    [InlineData("BOB@ReeInvent.com", "reeinvent.com")]
    [InlineData("no-domain", "")]
    [InlineData("trailing@", "")]
    public void GetEmailDomain_ReturnsLowercasedDomain(string email, string expected)
    {
        Assert.Equal(expected, InvitationTokenHelper.GetEmailDomain(email));
    }

    [Fact]
    public void IsEmailDomainAllowed_EmptyList_AllowsAnyDomain()
    {
        Assert.True(InvitationTokenHelper.IsEmailDomainAllowed("anyone@gmail.com", []));
    }

    [Theory]
    [InlineData("alice@reeinvent.com", true)]
    [InlineData("ALICE@ReeInvent.COM", true)]
    [InlineData("bob@reeinvent.io", true)]
    [InlineData("mallory@gmail.com", false)]
    [InlineData("no-domain", false)]
    public void IsEmailDomainAllowed_EnforcesConfiguredDomains(string email, bool expected)
    {
        string[] allowed = ["reeinvent.com", "@reeinvent.io"];

        Assert.Equal(expected, InvitationTokenHelper.IsEmailDomainAllowed(email, allowed));
    }
}
