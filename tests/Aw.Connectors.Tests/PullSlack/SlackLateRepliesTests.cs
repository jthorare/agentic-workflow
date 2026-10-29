using Aw.Connectors.Core;
using Aw.Connectors.Core.Slack;
using Aw.Connectors.Features;
using Aw.Connectors.Testing;
using Aw.Connectors.Testing.Slack;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Connectors.Tests.PullSlack;

// Issue #1689: a new reply in a thread whose root was already synced must reach the next incremental pull, even when no new top-level message was posted.
public sealed class SlackLateRepliesTests
{
    private static SlackChannelRef General => new("C0001GEN", "general");

    private static DateTimeOffset Monday => new(2026, 10, 26, 9, 0, 0, TimeSpan.Zero);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string At(DateTimeOffset at) => SlackTs.Format(at);

    private static IConnector Connector(FakeSlackReader reader)
    {
        var settings = SlackSettings.Create([General], "env:SLACK_READ_TOKEN", "example").Value;
        var services = new ServiceCollection();
        _ = services.AddSingleton<ISlackReader>(reader).AddConnectorsFeatures().AddSlackConnector(settings);
        return services.BuildServiceProvider().CreateScope().ServiceProvider.GetRequiredService<IConnector>();
    }

    private static void AddRoot(FakeSlackReader reader, DateTimeOffset root)
        => _ = reader.Add(General.Channel, A.SlackMessage(At(root)).WithText("root"));

    private static void AddReply(FakeSlackReader reader, DateTimeOffset root, DateTimeOffset at, string text)
        => _ = reader.Add(General.Channel, A.SlackMessage(At(at)).InThread(At(root)).WithText(text));

    private static async Task<List<ConnectorPage>> PullAll(IConnector connector, SourceCursor? start)
    {
        List<ConnectorPage> pages = [];
        var cursor = start;
        for (var i = 0; i < 50; i++)
        {
            var result = await connector.Pull(cursor, 100, before: null, Ct);
            Assert.False(result.IsError, "a pull must not fail: " + (result.IsError ? result.Error.Message : string.Empty));
            pages.Add(result.Value);
            cursor = result.Value.Next;
            if (result.Value.Items.Count == 0)
            {
                break;
            }
        }

        return pages;
    }

    private static void AssertThreadHasAllReplies(List<ConnectorPage> pages, DateTimeOffset root)
    {
        var key = "slack:" + General.Channel + "/" + At(root);
        var thread = pages.SelectMany(static p => p.Items).FirstOrDefault(i => i.Key == key);
        Assert.True(thread is not null, "the thread record " + key + " must reach the pull after a late reply; the pull emitted: " + string.Join(", ", pages.SelectMany(static p => p.Items).Select(static i => i.Key)));
        for (var n = 1; n <= 5; n++)
        {
            Assert.Contains("reply " + n.ToString(System.Globalization.CultureInfo.InvariantCulture), thread.Text, StringComparison.Ordinal);
        }

        Assert.Empty(pages[^1].Items);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_late_reply_on_an_already_read_thread_reaches_the_next_pull()
    {
        var reader = new FakeSlackReader(General.Channel);
        AddRoot(reader, Monday);
        AddReply(reader, Monday, Monday.AddMinutes(1), "reply 1");
        AddReply(reader, Monday, Monday.AddMinutes(2), "reply 2");
        _ = reader.Add(General.Channel, A.SlackMessage(At(Monday.AddHours(1))).WithText("unthreaded"));
        var connector = Connector(reader);
        var first = await PullAll(connector, start: null);
        Assert.Empty(first[^1].Items);

        var tuesday = Monday.AddDays(1);
        AddReply(reader, Monday, tuesday, "reply 3");
        AddReply(reader, Monday, tuesday.AddMinutes(1), "reply 4");
        AddReply(reader, Monday, tuesday.AddMinutes(2), "reply 5");
        var second = await PullAll(connector, first[^1].Next);

        AssertThreadHasAllReplies(second, Monday);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_late_reply_on_a_thread_from_an_older_day_than_the_watermark_reaches_the_next_pull()
    {
        var reader = new FakeSlackReader(General.Channel);
        AddRoot(reader, Monday);
        AddReply(reader, Monday, Monday.AddMinutes(1), "reply 1");
        AddReply(reader, Monday, Monday.AddMinutes(2), "reply 2");
        _ = reader.Add(General.Channel, A.SlackMessage(At(Monday.AddDays(2))).WithText("wednesday plain message"));
        var connector = Connector(reader);
        var first = await PullAll(connector, start: null);
        Assert.Empty(first[^1].Items);

        var thursday = Monday.AddDays(3);
        AddReply(reader, Monday, thursday, "reply 3");
        AddReply(reader, Monday, thursday.AddMinutes(1), "reply 4");
        AddReply(reader, Monday, thursday.AddMinutes(2), "reply 5");
        var second = await PullAll(connector, first[^1].Next);

        AssertThreadHasAllReplies(second, Monday);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_incremental_pull_with_no_new_replies_emits_nothing()
    {
        var reader = new FakeSlackReader(General.Channel);
        AddRoot(reader, Monday);
        AddReply(reader, Monday, Monday.AddMinutes(1), "reply 1");
        AddReply(reader, Monday, Monday.AddMinutes(2), "reply 2");
        _ = reader.Add(General.Channel, A.SlackMessage(At(Monday.AddHours(1))).WithText("unthreaded"));
        var connector = Connector(reader);
        var first = await PullAll(connector, start: null);
        Assert.Empty(first[^1].Items);

        var second = await PullAll(connector, first[^1].Next);

        var only = Assert.Single(second);
        Assert.Empty(only.Items);
    }
}
