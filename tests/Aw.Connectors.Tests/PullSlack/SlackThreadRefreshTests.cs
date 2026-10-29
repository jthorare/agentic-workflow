using Aw.Connectors.Core;
using Aw.Connectors.Core.Slack;
using Aw.Connectors.Features;
using Aw.Connectors.Testing;
using Aw.Connectors.Testing.Slack;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Connectors.Tests.PullSlack;

// Issue #1689: the thread refresh resumes after a rate limit and only looks a bounded number of days back.
public sealed class SlackThreadRefreshTests
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
}
