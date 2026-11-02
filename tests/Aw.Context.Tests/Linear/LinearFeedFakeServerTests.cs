using System.Globalization;
using Aw.Context.Core.Records;
using Aw.Context.Core.Sync;
using Aw.Context.Testing.Fakes;

namespace Aw.Context.Tests.Linear;

/// <summary>
/// The Linear record feed pulled through the fake Linear server, so the IssuesPull query itself is checked against the recordings (the handler behind
/// the contract tests never reads the query): every issue comes back, with the canceled time, the parent, the relations and the comments order the query asks for.
/// </summary>
[Trait("Lane", "integration")]
public sealed class LinearFeedFakeServerTests : IAsyncDisposable
{
    private const string Prefix = "0e5a1b00-0000-4000-8000-";
    private readonly List<(FakeLinearServer Server, LinearSourceHarness Harness)> _created = [];

    [Fact]
    public async Task The_pull_returns_all_the_split_panes_issues()
    {
        var items = await Pull();

        Assert.Equal(6, items.Count);
        Assert.Equal(Enumerable.Range(101, 6).Select(Ref), items.Select(i => i.Key.Value).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public async Task A_canceled_issue_is_canceled_and_closes_at_its_canceledAt()
    {
        var item = One(await Pull(), 106);

        Assert.Equal((RecordState.Canceled, new DateTimeOffset(2026, 8, 20, 10, 0, 0, TimeSpan.Zero)), (item.State, item.ClosedAt));
    }

    [Fact]
    public async Task An_issue_with_a_parent_links_to_it()
    {
        var item = One(await Pull(), 102);

        Assert.Contains(item.Links, l => l.Kind == RecordLinkKind.Parent && l.ToRef == Ref(101));
    }

    [Fact]
    public async Task A_blocks_relation_links_the_blocker_to_the_blocked_issue()
    {
        var items = await Pull();

        Assert.Contains(One(items, 101).Links, l => l.Kind == RecordLinkKind.Blocks && l.ToRef == Ref(102));
    }

    [Fact]
    public async Task A_related_pair_links_both_ways()
    {
        var items = await Pull();

        Assert.Contains(One(items, 103).Links, l => l.Kind == RecordLinkKind.Relates && l.ToRef == Ref(104));
        Assert.Contains(One(items, 104).Links, l => l.Kind == RecordLinkKind.Relates && l.ToRef == Ref(103));
    }

    [Fact]
    public async Task The_comments_come_newest_first()
    {
        var text = One(await Pull(), 103).Text;

        var newest = text.IndexOf("(2026-07-23)", StringComparison.Ordinal);
        var oldest = text.IndexOf("(2026-07-08)", StringComparison.Ordinal);
        Assert.True(newest >= 0 && oldest >= 0 && newest < oldest, text);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var (server, harness) in _created)
        {
            harness.Dispose();
            await server.DisposeAsync();
        }
    }

    private static string Ref(int number) => string.Create(CultureInfo.InvariantCulture, $"linear:issue/{Prefix}000000000{number}");

    private static RecordItem One(IReadOnlyList<RecordItem> items, int number) => Assert.Single(items, i => i.Key.Value == Ref(number));

    private async Task<IReadOnlyList<RecordItem>> Pull()
    {
        var linear = await FakeLinearServer.Start();
        var harness = new LinearSourceHarness(linear.Url, linear.Token);
        _created.Add((linear, harness));
        var feed = harness.Sources.CreateFeed(harness.Settings);

        var page = await feed.Pull(new RecordPull(After: null, 25, NotBefore: null), TestContext.Current.CancellationToken);

        return page.Value.Items;
    }
}
