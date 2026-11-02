using Aw.Connectors.Core;
using Aw.Connectors.Core.Slack;
using Aw.Connectors.Testing;

namespace Aw.Connectors.Tests.Slack;

/// <summary>What every <see cref="ISlackReader"/> does, run against the fake and the real client so the fake stays honest.</summary>
public abstract class SlackReaderContract
{
    protected const string General = "C0001GEN";
    protected const string Other = "C0009OTH";
    protected const string DirectMessage = "D0003DMX";

    protected static DateTimeOffset T => new(2026, 10, 27, 9, 0, 0, TimeSpan.Zero);

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected static string At(int minutes) => SlackTs.Format(T.AddMinutes(minutes));

    /// <summary>A reader that may read <paramref name="allowed"/> and holds <paramref name="messages"/> (channel, message).</summary>
    protected abstract ISlackReader Reader(IReadOnlyList<string> allowed, IReadOnlyList<(string Channel, SlackMessage Message)> messages);

    private ISlackReader Reader(params SlackMessage[] messages) => Reader([General], [.. messages.Select(static m => (General, m))]);

    [Fact, Trait("Lane", "unit")]
    public async Task History_comes_newest_first_and_holds_only_top_level_messages()
    {
        var reader = Reader(
            A.SlackMessage(At(1)).WithText("first"),
            A.SlackMessage(At(3)).WithText("third"),
            A.SlackMessage(At(2)).WithText("second"),
            A.SlackMessage(At(4)).InThread(At(1)).WithText("a reply"));

        var page = (await reader.History(General, oldest: null, latest: null, Ct)).Value;

        Assert.Equal(["third", "second", "first"], page.Items.Select(static m => m.Text), StringComparer.Ordinal);
        Assert.Null(page.RateLimitedUntil);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task History_starts_at_oldest_inclusive()
    {
        var reader = Reader(A.SlackMessage(At(1)), A.SlackMessage(At(2)), A.SlackMessage(At(3)));

        var page = (await reader.History(General, At(2), latest: null, Ct)).Value;

        Assert.Equal([At(3), At(2)], page.Items.Select(static m => m.Ts), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task History_stops_before_latest_exclusive()
    {
        var reader = Reader(A.SlackMessage(At(1)), A.SlackMessage(At(2)), A.SlackMessage(At(3)));

        var page = (await reader.History(General, oldest: null, At(3), Ct)).Value;

        Assert.Equal([At(2), At(1)], page.Items.Select(static m => m.Ts), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_thread_root_reports_its_replies_and_its_own_ts_as_thread_ts()
    {
        var reader = Reader(A.SlackMessage(At(1)).WithText("root"), A.SlackMessage(At(2)).InThread(At(1)), A.SlackMessage(At(3)).InThread(At(1)));

        var root = Assert.Single((await reader.History(General, oldest: null, latest: null, Ct)).Value.Items);

        Assert.Equal(2, root.ReplyCount);
        Assert.Equal(At(1), root.ThreadTs);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_thread_comes_root_first_then_its_replies_oldest_first_and_only_its_own()
    {
        var reader = Reader(
            A.SlackMessage(At(3)).InThread(At(1)).WithText("reply two"),
            A.SlackMessage(At(1)).WithText("root"),
            A.SlackMessage(At(2)).InThread(At(1)).WithText("reply one"),
            A.SlackMessage(At(5)).WithText("elsewhere"),
            A.SlackMessage(At(6)).InThread(At(5)).WithText("elsewhere reply"));

        var page = (await reader.Replies(General, At(1), Ct)).Value;

        Assert.Equal(["root", "reply one", "reply two"], page.Items.Select(static m => m.Text), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_author_is_the_display_name_and_null_when_the_payload_gives_none()
    {
        var reader = Reader(A.SlackMessage(At(2)).By("Bo"), A.SlackMessage(At(1)).By(author: null));

        var items = (await reader.History(General, oldest: null, latest: null, Ct)).Value.Items;

        Assert.Equal(["Bo", null], items.Select(static m => m.Author), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_channel_that_is_not_allowed_is_refused_before_anything_is_read()
    {
        var reader = Reader([General], [(General, A.SlackMessage(At(1))), (Other, A.SlackMessage(At(2)))]);

        Assert.Equal(ConnectorsErrors.ChannelNotAllowed.Code, (await reader.History(Other, oldest: null, latest: null, Ct)).Error.Code);
        Assert.Equal(ConnectorsErrors.ChannelNotAllowed.Code, (await reader.Replies(Other, At(2), Ct)).Error.Code);
        Assert.Equal(ConnectorsErrors.ChannelNotAllowed.Code, (await reader.Probe(Other, Ct)).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_direct_message_is_refused_even_when_it_is_listed()
    {
        var reader = Reader([General, DirectMessage], [(General, A.SlackMessage(At(1)))]);

        Assert.Equal(ConnectorsErrors.ChannelNotAllowed.Code, (await reader.History(DirectMessage, oldest: null, latest: null, Ct)).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_probe_succeeds_on_a_readable_channel()
    {
        var result = await Reader(A.SlackMessage(At(1))).Probe(General, Ct);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }
}
