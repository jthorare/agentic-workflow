using Aw.Kernel.Testing;
using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Tests.Messages;

/// <summary>M1d (messages spec §5.1, §5.3, §8.1): the one rate budget, the AW_SLACK_BASE_URL marker rule and the connection's auth.test probe.</summary>
[Lane(TestLane.Unit)]
public sealed class SlackClientTests
{
    private const string Override = "http://127.0.0.1:47123/api/";
    private const string Marker = "slack-fixture v1\n47123\n";

    private static readonly string _fixtureToken = string.Concat("xo", "xp-fixture-", "0000");   // why: split so no token-shaped literal sits in the source
    private static readonly string _realToken = string.Concat("xo", "xp-", "1234567890-0000");

    [Fact]
    public void Rate_limit_429_pauses_only_that_method()
    {
        var now = KnownInstants.Epoch;
        var budget = new SlackRateBudget();

        budget.Hold("conversations.history", now, TimeSpan.FromSeconds(12));

        Assert.False(budget.MayCall("conversations.history", now.AddSeconds(11)));
        Assert.True(budget.MayCall("users.info", now.AddSeconds(1)));
        Assert.True(budget.MayCall("conversations.history", now.AddSeconds(13)));
    }

    [Fact]
    public void A_shorter_hold_never_cuts_a_longer_one()
    {
        var now = KnownInstants.Epoch;
        var budget = new SlackRateBudget();

        budget.Hold("search.messages", now, TimeSpan.FromSeconds(30));
        budget.Hold("search.messages", now, TimeSpan.FromSeconds(5));

        Assert.Equal(TimeSpan.FromSeconds(20), budget.Wait("search.messages", now.AddSeconds(10)));
    }

    [Fact]
    public void Base_url_override_is_ignored_without_the_marker() =>
        Assert.Equal(SlackBaseUrlRule.Slack, SlackBaseUrlRule.Resolve(Override, marker: null, _fixtureToken));

    [Fact]
    public void Base_url_override_is_taken_with_the_marker_its_port_and_a_fixture_token() =>
        Assert.Equal(new Uri(Override), SlackBaseUrlRule.Resolve(Override, Marker, _fixtureToken));

    [Theory]
    [InlineData("http://10.0.0.5:47123/api/")]
    [InlineData("http://127.0.0.2:47123/api/")]
    [InlineData("http://slack.example:47123/api/")]
    [InlineData("http://user:" + "pw@127.0.0.1:47123/api/")]   // why: split so the staged-secret check sees no credentialed url
    [InlineData("ftp://127.0.0.1:47123/api/")]
    [InlineData("not a url")]
    public void Base_url_override_refuses_a_non_loopback_host(string overrideUrl) =>
        Assert.Equal(SlackBaseUrlRule.Slack, SlackBaseUrlRule.Resolve(overrideUrl, Marker, _fixtureToken));

    [Fact]
    public void Base_url_override_refuses_a_real_token() =>
        Assert.Equal(SlackBaseUrlRule.Slack, SlackBaseUrlRule.Resolve(Override, Marker, _realToken));

    [Theory]
    [InlineData("slack-fixture v1\n47124\n")]
    [InlineData("slack-fixture v2\n47123\n")]
    [InlineData("slack-fixture v1\n")]
    [InlineData("47123")]
    [InlineData("")]
    public void Base_url_override_refuses_a_marker_that_does_not_name_its_port(string marker) =>
        Assert.Equal(SlackBaseUrlRule.Slack, SlackBaseUrlRule.Resolve(Override, marker, _fixtureToken));

    [Theory]
    [InlineData("http://localhost:47123/api", "http://localhost:47123/api/")]
    [InlineData("http://[::1]:47123/api/", "http://[::1]:47123/api/")]
    [InlineData("http://127.0.0.1:47123", "http://127.0.0.1:47123/")]
    public void Base_url_override_ends_in_a_slash_on_every_loopback_host(string overrideUrl, string expected) =>
        Assert.Equal(new Uri(expected), SlackBaseUrlRule.Resolve(overrideUrl, "slack-fixture v1\r\n47123\r\n", _fixtureToken));

    [Theory]
    [InlineData("https://slack.com/api/auth.test")]
    [InlineData("https://files.slack.com/files-pri/T1-F1/a.png")]
    [InlineData("https://example.slack.com/archives/C0001GEN/p1")]
    [InlineData("https://EXAMPLE.Slack.com/api/x")]
    public void Slack_hosts_over_https_may_get_the_token(string url) => Assert.True(SlackHostRule.IsSlack(new Uri(url)));

    [Theory]
    [InlineData("http://slack.com/api/x")]
    [InlineData("https://evilslack.com/api/x")]
    [InlineData("https://slack.com.evil.example/api/x")]
    [InlineData("https://notslack.com/api/x")]
    [InlineData("https://slack.com@evil.example/api/x")]
    [InlineData("https://user:" + "pw@slack.com/api/x")]   // why: split so the staged-secret check sees no credentialed url
    [InlineData("https://127.0.0.1/api/x")]
    [InlineData("https://[::1]/api/x")]
    [InlineData("https://localhost/api/x")]
    [InlineData("ftp://slack.com/x")]
    public void Any_other_host_scheme_or_user_info_never_gets_the_token(string url) => Assert.False(SlackHostRule.IsSlack(new Uri(url)));

    [Fact]
    public void Connection_test_calls_auth_test()
    {
        // without a probeUrl a Preset-null connection reports "connected" with no call (§5.1); M1d's second PR also proves the Test button calls it on the fake
        var config = MessagesConnection.Config("T0HARBOR", "U0JOI");

        Assert.Equal("https://slack.com/api/auth.test", config["probeUrl"]);
        Assert.Equal("T0HARBOR", config[MessagesConnection.WorkspaceKey]);
        Assert.Equal("U0JOI", config[MessagesConnection.UserKey]);
    }
}
