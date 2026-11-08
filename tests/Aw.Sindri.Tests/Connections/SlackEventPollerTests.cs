using Aw.Kernel.Testing;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Connections;

[Lane(TestLane.Unit)]
public sealed class SlackEventPollerTests
{
    private const string Before = "1731059999.000000";

    [Fact]
    public async Task A_message_matching_the_filter_and_a_mention_of_the_owner_map_to_their_catalog_events_with_stable_delivery_keys()
    {
        var provider = new FakeProvider();
        provider.AddSlackMessage(ProviderFixtures.SlackMessage("1731060000.000100", "U2", "deploy failed on prod"));
        provider.AddSlackMessage(ProviderFixtures.SlackMessage("1731060000.000200", "U3", "lunch?"));
        provider.AddSlackMessage(ProviderFixtures.SlackMessage("1731060000.000300", "U4", "ping <@UOWNER> please look"));
        using var h = PollerHarness.For("slack", provider);
        var ct = TestContext.Current.CancellationToken;

        var message = await h.Poller.Poll(h.Connection, EventKind.SlackMessage, cursor: Before, ct);
        var mention = await h.Poller.Poll(h.Connection, EventKind.SlackMention, cursor: Before, ct);
        var again = await h.Poller.Poll(h.Connection, EventKind.SlackMessage, cursor: Before, ct);
        var caughtUp = await h.Poller.Poll(h.Connection, EventKind.SlackMessage, cursor: "1731060000.000300", ct);

        var matched = Assert.Single(message.Value.Events);
        Assert.Equal("slack:C123:1731060000.000100", matched.DeliveryKey);
        Assert.Equal(EventKind.SlackMessage, matched.Kind);
        Assert.Equal("C123", matched.Fields["channel"]);
        Assert.Equal("U2", matched.Fields["user"]);
        Assert.Equal("deploy failed on prod", matched.Fields["text"]);
        Assert.Equal("1731060000.000100", matched.Fields["ts"]);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1731060000), matched.OccurredAt);
        Assert.Equal("1731060000.000300", message.Value.NextCursor);

        var mentioned = Assert.Single(mention.Value.Events);
        Assert.Equal("slack:C123:1731060000.000300", mentioned.DeliveryKey);
        Assert.Equal(EventKind.SlackMention, mentioned.Kind);

        Assert.Equal(matched.DeliveryKey, Assert.Single(again.Value.Events).DeliveryKey);
        Assert.Empty(caughtUp.Value.Events);
        Assert.Equal("1731060000.000300", caughtUp.Value.NextCursor);
        Assert.All(provider.Requests, static r => Assert.Contains("channel=C123", r.Uri.Query, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Without_a_filter_every_message_in_the_channel_matches()
    {
        var provider = new FakeProvider();
        provider.AddSlackMessage(ProviderFixtures.SlackMessage("1731060000.000100", "U2", "anything"));
        using var h = PollerHarness.For("slack", provider, new Dictionary<string, string>(StringComparer.Ordinal) { ["filter"] = string.Empty });

        var message = await h.Poller.Poll(h.Connection, EventKind.SlackMessage, Before, TestContext.Current.CancellationToken);

        _ = Assert.Single(message.Value.Events);
    }

    [Fact]
    public async Task Slack_ratelimited_is_a_rate_limited_result_that_keeps_the_cursor()
    {
        var provider = new FakeProvider();
        provider.SlackError("ratelimited");
        using var h = PollerHarness.For("slack", provider);

        var polled = await h.Poller.Poll(h.Connection, EventKind.SlackMessage, cursor: "1731060000.000100", TestContext.Current.CancellationToken);

        Assert.True(polled.IsSuccess);
        Assert.True(polled.Value.RateLimited);
        Assert.Empty(polled.Value.Events);
        Assert.Equal("1731060000.000100", polled.Value.NextCursor);
    }

    [Theory]
    [InlineData("invalid_auth", "SND-CONN-002")]
    [InlineData("token_revoked", "SND-CONN-002")]
    [InlineData("channel_not_found", "SND-CONN-012")]
    public async Task Slack_errors_in_a_200_answer_map_to_the_connection_error_codes(string slackError, string code)
    {
        var provider = new FakeProvider();
        provider.SlackError(slackError);
        using var h = PollerHarness.For("slack", provider);

        var polled = await h.Poller.Poll(h.Connection, EventKind.SlackMessage, cursor: null, TestContext.Current.CancellationToken);

        Assert.True(polled.IsError);
        Assert.Equal(code, polled.Error.Code);
        Assert.DoesNotContain(PollerHarness.Token, polled.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_first_poll_is_a_baseline_with_no_events_and_the_newest_ts_as_the_cursor()
    {
        var provider = new FakeProvider();
        provider.AddSlackMessage(ProviderFixtures.SlackMessage("1731060000.000100", "U2", "deploy failed on prod"));
        using var h = PollerHarness.For("slack", provider);
        using var empty = PollerHarness.For("slack", new FakeProvider());
        var ct = TestContext.Current.CancellationToken;

        var baseline = await h.Poller.Poll(h.Connection, EventKind.SlackMessage, cursor: null, ct);
        var onEmpty = await empty.Poller.Poll(empty.Connection, EventKind.SlackMessage, cursor: null, ct);

        Assert.Empty(baseline.Value.Events);
        Assert.Equal("1731060000.000100", baseline.Value.NextCursor);
        Assert.Empty(onEmpty.Value.Events);
        Assert.Equal("1767225600.000000", onEmpty.Value.NextCursor);   // 2026-01-01T00:00:00Z, the test clock
    }
}
