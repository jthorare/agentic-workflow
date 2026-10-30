using Aw.Connectors.Core;
using Aw.Connectors.Core.Slack;
using Aw.Connectors.Features;
using Aw.Connectors.Testing;
using Aw.Connectors.Testing.Slack;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Connectors.Tests.PullSlack;

// Issue #1679: a rate limit partway through a channel must keep the threads already read, and the next pull must resume after them.
public sealed class SlackResumeTests
{
    private static SlackChannelRef General => new("C0001GEN", "general");

    private static DateTimeOffset FirstDay => new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);

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
    {
        _ = reader
            .Add(General.Channel, A.SlackMessage(At(root)).WithText("root " + At(root)))
            .Add(General.Channel, A.SlackMessage(At(root.AddSeconds(30))).InThread(At(root)).WithText("reply"));
    }

    private static async Task<List<ConnectorPage>> PullAll(IConnector connector, SourceCursor? start, int limit, int cap)
    {
        List<ConnectorPage> pages = [];
        var cursor = start;
        for (var i = 0; i < cap; i++)
        {
            var result = await connector.Pull(cursor, limit, before: null, Ct);
            Assert.False(result.IsError, "a pull must not fail: " + (result.IsError ? result.Error.Message : string.Empty));
            pages.Add(result.Value);
            if (result.Value.Items.Count == 0)
            {
                break;
            }

            cursor = result.Value.Next;
        }

        return pages;
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_rate_limit_partway_through_a_channel_keeps_the_threads_already_read_and_resumes_after_them()
    {
        var reader = new FakeSlackReader(General.Channel);
        List<string> threadKeys = [];
        List<string> dayKeys = [];
        for (var day = 0; day < 3; day++)
        {
            var start = FirstDay.AddDays(day);
            dayKeys.Add("slack:C0001GEN/day/" + start.UtcDateTime.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
            for (var t = 0; t < 10; t++)
            {
                AddThread(reader, start.AddMinutes(t * 10));
                threadKeys.Add("slack:C0001GEN/" + At(start.AddMinutes(t * 10)));
            }

            _ = reader
                .Add(General.Channel, A.SlackMessage(At(start.AddMinutes(101))).WithText("loose one"))
                .Add(General.Channel, A.SlackMessage(At(start.AddMinutes(102))).WithText("loose two"));
        }

        var connector = Connector(reader);
        _ = reader.RateLimitRepliesAfter(10, new DateTimeOffset(2026, 10, 28, 12, 0, 0, TimeSpan.Zero));

        var first = await connector.Pull(after: null, 100, before: null, Ct);

        Assert.False(first.IsError, "the first pull must keep its progress, got: " + (first.IsError ? first.Error.Code + " " + first.Error.Message : string.Empty));
        var firstThreads = first.Value.Items.Where(static i => !i.Key.Contains("/day/", StringComparison.Ordinal)).Select(static i => i.Key).ToList();
        Assert.NotEmpty(firstThreads);
        Assert.NotEqual(string.Empty, first.Value.Next.Value, StringComparer.Ordinal);
        var readsAfterFirst = reader.Reads.Count;

        _ = reader.RateLimitRepliesAfter(0, until: null);
        var rest = await PullAll(connector, first.Value.Next, 100, 50);

        var all = first.Value.Items.Concat(rest.SelectMany(static p => p.Items)).ToList();
        var threadsSeen = all.Where(static i => !i.Key.Contains("/day/", StringComparison.Ordinal)).Select(static i => i.Key).ToList();
        Assert.Equal(threadKeys.Order(StringComparer.Ordinal), threadsSeen.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        // why: a day record is rebuilt from its first message when a pull resumes inside that day, so a day may come again; it must come at least once.
        Assert.Equal(dayKeys.Order(StringComparer.Ordinal), all.Select(static i => i.Key).Where(static k => k.Contains("/day/", StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        var rereads = reader.Reads.Skip(readsAfterFirst).Where(r => firstThreads.Exists(k => r == "replies C0001GEN " + k["slack:C0001GEN/".Length..])).ToList();
        Assert.Empty(rereads);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_channel_bigger_than_the_limit_returns_bounded_pulls_that_together_read_it_all()
    {
        const int limit = 20;
        var reader = new FakeSlackReader(General.Channel);
        List<string> expected = [];
        for (var t = 0; t < 150; t++)
        {
            AddThread(reader, FirstDay.AddMinutes(t));
            expected.Add("slack:C0001GEN/" + At(FirstDay.AddMinutes(t)));
        }

        var pages = await PullAll(Connector(reader), start: null, limit, cap: 100);

        Assert.All(pages, p => Assert.True(p.Items.Count <= 2 * limit, $"a pull returned {p.Items.Count} items for a limit of {limit}"));
        Assert.All(pages.SkipLast(1), p => Assert.NotEmpty(p.Items));
        Assert.Empty(pages[^1].Items);
        var seen = pages.SelectMany(static p => p.Items).Select(static i => i.Key).ToList();

        // why: a pass that spans several pulls moves no replies mark (#1689), so the next thread refresh reads its newest thread once more; the record is the same key, a consumer upserts it.
        Assert.Equal(expected.Order(StringComparer.Ordinal), seen.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.True(seen.Count - expected.Count <= 1, "at most the newest thread is read twice");
    }
}
