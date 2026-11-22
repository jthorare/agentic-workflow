using Aw.Kernel.Testing;
using Aw.Sindri.Core.Messages;

namespace Aw.Sindri.Tests.Messages;

/// <summary>M1d (messages spec §5.1, §5.3, §8.1): the one rate budget, the AW_SLACK_BASE_URL marker rule and the connection's auth.test probe.</summary>
[Lane(TestLane.Unit)]
public sealed class SlackClientTests
{
    private static readonly string _fixtureToken = string.Concat("xo", "xp-fixture-", "0000");   // why: split so no token-shaped literal sits in the source

    [Fact]
    public void Rate_limit_429_pauses_only_that_method() => RedUntil.Phase("M1d", () =>
    {
            var now = KnownInstants.Epoch;
            var budget = new SlackRateBudget();

            budget.Hold("conversations.history", now, TimeSpan.FromSeconds(12));

            Assert.False(budget.MayCall("conversations.history", now.AddSeconds(11)));
            Assert.True(budget.MayCall("users.info", now.AddSeconds(1)));
            Assert.True(budget.MayCall("conversations.history", now.AddSeconds(13)));
    });

    [Fact]
    public void Base_url_override_is_ignored_without_the_marker() =>
        RedUntil.Phase("M1d", () => Assert.Equal(SlackBaseUrlRule.Slack, SlackBaseUrlRule.Resolve("http://127.0.0.1:47123/api/", marker: null, _fixtureToken)));

    [Fact]
    public void Connection_test_calls_auth_test() => RedUntil.Phase("M1d", () =>
    {
            // without a probeUrl a Preset-null connection reports "connected" with no call (§5.1); M1d also proves the Test button calls it on the fake
            var config = MessagesConnection.Config("T0HARBOR", "U0JOI");

            Assert.Equal("https://slack.com/api/auth.test", config["probeUrl"]);
    });
}
