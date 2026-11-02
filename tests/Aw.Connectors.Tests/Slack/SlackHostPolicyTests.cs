using Aw.Connectors.Infrastructure.Slack;

namespace Aw.Connectors.Tests.Slack;

public sealed class SlackHostPolicyTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("https://slack.com/api/conversations.history")]
    [InlineData("https://www.slack.com/api/x")]
    [InlineData("https://example.slack.com/archives/C0001GEN/p1")]
    [InlineData("https://EXAMPLE.Slack.com/api/x")]
    public void Slack_hosts_over_https_are_allowed(string url) => Assert.True(SlackHostPolicy.IsAllowed(new Uri(url)));

    [Theory, Trait("Lane", "unit")]
    [InlineData("http://slack.com/api/x")]
    [InlineData("https://evilslack.com/api/x")]
    [InlineData("https://slack.com.evil.example/api/x")]
    [InlineData("https://notslack.com/api/x")]
    [InlineData("https://slack.com@evil.example/api/x")]
    [InlineData("https://user:pw@slack.com/api/x")]
    [InlineData("https://127.0.0.1/api/x")]
    [InlineData("https://[::1]/api/x")]
    [InlineData("https://localhost/api/x")]
    [InlineData("ftp://slack.com/x")]
    public void Any_other_host_scheme_or_user_info_is_refused(string url) => Assert.False(SlackHostPolicy.IsAllowed(new Uri(url)));
}
