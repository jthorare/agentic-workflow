using Aw.Connectors.Core;
using Aw.Connectors.Core.Slack;
using Aw.Connectors.Features;
using Aw.Connectors.Testing;
using Aw.Connectors.Testing.Slack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Connectors.Tests.PullSlack;

// Issue #1689: the thread refresh resumes after a rate limit and only looks a bounded number of days back.
public sealed class SlackThreadRefreshTests
{
    private static SlackChannelRef General => new("C0001GEN", "general");

    private static DateTimeOffset Monday => new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string At(DateTimeOffset at) => SlackTs.Format(at);

    private static IConnector Connector(FakeSlackReader reader, FakeTimeProvider? clock = null)
    {
        var settings = SlackSettings.Create([General], "env:SLACK_READ_TOKEN", "example").Value;
        var services = new ServiceCollection();
        if (clock is not null)
        {
            _ = services.AddSingleton<TimeProvider>(clock);
        }

        _ = services.AddSingleton<ISlackReader>(reader).AddConnectorsFeatures().AddSlackConnector(settings);
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IConnector>();
    }

    private static void AddThread(FakeSlackReader reader, DateTimeOffset root)
        => _ = reader
            .Add(General.Channel, A.SlackMessage(At(root)).WithText("root"))
            .Add(General.Channel, A.SlackMessage(At(root.AddMinutes(1))).InThread(At(root)).WithText("first"));

    private static void Reply(FakeSlackReader reader, DateTimeOffset root, DateTimeOffset at, string text)
        => _ = reader.Add(General.Channel, A.SlackMessage(At(at)).InThread(At(root)).WithText(text));

    private static async Task<(List<string> Keys, SourceCursor? Next)> Drain(IConnector connector, SourceCursor? start)
    {
        List<string> keys = [];
        var cursor = start;
        for (var i = 0; i < 20; i++)
        {
            var pulled = await connector.Pull(cursor, 100, before: null, Ct);
            if (pulled.IsError)
            {
                break; // why: a rate limit surfaces as an error on the pull that read nothing; the cursor of the last page holds the place.
            }

            var page = pulled.Value;
            keys.AddRange(page.Items.Select(static item => item.Key));
            if (page.Items.Count == 0)
            {
                break;
            }

            cursor = page.Next;
        }

        return (keys, cursor);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rate_limited_refresh_keeps_what_it_read_and_resumes()
    {
        var reader = new FakeSlackReader(General.Channel);
        var second = Monday.AddHours(1);
        AddThread(reader, Monday);
        AddThread(reader, second);
        var connector = Connector(reader);
        var (_, afterFirst) = await Drain(connector, start: null);

        Reply(reader, Monday, Monday.AddDays(1), "late a");
        Reply(reader, second, Monday.AddDays(1).AddMinutes(5), "late b");
        _ = reader.RateLimitRepliesAfter(1, Monday.AddDays(30));
        var (limitedKeys, afterLimited) = await Drain(connector, afterFirst);
        Assert.Equal(["slack:" + General.Channel + "/" + At(Monday)], limitedKeys);

        _ = reader.RateLimitRepliesAfter(0, until: null);
        var (resumedKeys, _) = await Drain(connector, afterLimited);
        Assert.Equal(["slack:" + General.Channel + "/" + At(second)], resumedKeys);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_thread_older_than_the_refresh_window_is_not_read_again()
    {
        var reader = new FakeSlackReader(General.Channel);
        AddThread(reader, Monday);
        _ = reader.Add(General.Channel, A.SlackMessage(At(Monday.AddDays(10))).WithText("plain"));
        var connector = Connector(reader);
        var (_, afterFirst) = await Drain(connector, start: null);

        Reply(reader, Monday, Monday.AddDays(11), "very late");
        var (laterKeys, _) = await Drain(connector, afterFirst);

        Assert.Empty(laterKeys);
        Assert.Equal(1, reader.Reads.Count(r => r == "replies " + General.Channel + " " + At(Monday)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_reply_landing_on_a_candidate_before_its_read_does_not_hide_the_next_candidate()
    {
        var reader = new FakeSlackReader(General.Channel);
        var b = Monday.AddHours(1);
        AddThread(reader, Monday);
        AddThread(reader, b);
        var connector = Connector(reader, new FakeTimeProvider(Monday.AddDays(2)));
        var (_, afterSync) = await Drain(connector, start: null);

        Reply(reader, Monday, Monday.AddDays(1), "late a");
        Reply(reader, b, Monday.AddDays(1).AddMinutes(5), "late b");
        var landed = false;
        reader.BeforeReplies = ts =>
        {
            if (!landed && string.Equals(ts, At(Monday), StringComparison.Ordinal))
            {
                landed = true;
                Reply(reader, Monday, Monday.AddDays(1).AddMinutes(9), "landed during the read"); // why: after the scan, before A's read
            }
        };
        _ = reader.RateLimitRepliesAfter(1, Monday.AddDays(30));
        var (firstKeys, afterFirst) = await Drain(connector, afterSync);
        Assert.Equal(["slack:" + General.Channel + "/" + At(Monday)], firstKeys);

        reader.BeforeReplies = null;
        _ = reader.RateLimitRepliesAfter(0, until: null);
        var (resumedKeys, _) = await Drain(connector, afterFirst);

        Assert.Contains("slack:" + General.Channel + "/" + At(b), resumedKeys, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_reply_on_an_old_thread_landing_while_a_pass_reads_a_new_thread_is_read_next_pull()
    {
        var reader = new FakeSlackReader(General.Channel);
        var now = Monday.AddDays(2);
        AddThread(reader, Monday);
        var connector = Connector(reader, new FakeTimeProvider(now));
        var (_, afterSync) = await Drain(connector, start: null);

        var x = now.AddMinutes(-3);
        _ = reader.Add(General.Channel, A.SlackMessage(At(x)).WithText("new root"));
        Reply(reader, x, now.AddMinutes(-2), "x reply");
        reader.BeforeReplies = ts =>
        {
            if (string.Equals(ts, At(x), StringComparison.Ordinal))
            {
                Reply(reader, Monday, now.AddMinutes(-2).AddSeconds(-30), "y reply landed"); // why: after the refresh scan, before the pass read X
            }
        };
        var (passKeys, afterPass) = await Drain(connector, afterSync);
        Assert.Contains("slack:" + General.Channel + "/" + At(x), passKeys, StringComparer.Ordinal);

        reader.BeforeReplies = null;
        var (nextKeys, _) = await Drain(connector, afterPass);

        // why: Drain goes on until an empty page, so the pull that reads Y's late reply may already be inside the first drain.
        var old = "slack:" + General.Channel + "/" + At(Monday);
        Assert.Contains(old, passKeys.Concat(nextKeys), StringComparer.Ordinal);
        Assert.True(reader.Reads.Where(r => r == "replies " + General.Channel + " " + At(Monday)).Skip(2).Any(), "the thread must be read again after the reply landed");
    }

    [Fact, Trait("Lane", "unit")]
    public async Task After_a_full_sync_with_nothing_new_further_pulls_are_empty_and_the_mark_stays()
    {
        var reader = new FakeSlackReader(General.Channel);
        AddThread(reader, Monday);
        var connector = Connector(reader, new FakeTimeProvider(Monday.AddDays(2)));
        var (_, afterSync) = await Drain(connector, start: null);

        var one = (await connector.Pull(afterSync, 100, before: null, Ct)).Value;
        var two = (await connector.Pull(one.Next, 100, before: null, Ct)).Value;

        Assert.Empty(one.Items);
        Assert.Empty(two.Items);
        Assert.EndsWith("~" + At(Monday.AddMinutes(1)), two.Next!.Value, StringComparison.Ordinal);
    }
}
