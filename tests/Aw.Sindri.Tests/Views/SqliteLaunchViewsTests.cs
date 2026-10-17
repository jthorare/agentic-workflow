using Aw.Kernel.Ids;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Views;

/// <summary>The launches view in views.db (API P10a): rows the real projection folds are read back by id, by item and by repo, newest request first, through the generated item/state/repo columns.</summary>
[Lane(TestLane.Integration)]
public sealed class SqliteLaunchViewsTests : IDisposable
{
    private static readonly RepoName _demo = RepoName.From("demo");
    private static readonly ItemId _other = ItemId.From("ITM-" + new string('2', 32));
    private readonly TempHome _home = new();
    private readonly SliceHarness _h = new();
    private readonly SqliteViews _views;
    private long _sequence;

    public SqliteLaunchViewsTests()
    {
        _views = new SqliteViews(Path.Combine(_home.Path, "home"));
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _views.Dispose();
        _home.Dispose();
    }

    private static LaunchId Id(int n) => LaunchId.From("LCH-" + n.ToString("x32", System.Globalization.CultureInfo.InvariantCulture));

    private async Task Apply<TPayload>(string type, TPayload payload, ItemId item)
    {
        _ = (await _views.Store.Open(ViewSchema.Version, Token)).Value;
        IProjection[] projections = [new ItemsProjection(_h.Events, new PassThroughScrubber()), new LaunchesProjection(_h.Events)];
        var applied = (await _views.Store.Apply([Recorded.Of(_h.Events, type, payload, ++_sequence, item)], projections, _h.Clock.GetUtcNow(), Token)).Value;
        Assert.Null(applied.Stall);
        _h.Clock.Advance(TimeSpan.FromSeconds(1));
    }

    private Task Item(ItemId item, RepoName repo) =>
        Apply(EventTypes.ItemCreated, new ItemCreated(_h.Ids.New<CommandId>(), item, "Ship it", repo, Link: null, Body: null, Owner: null), item);

    private Task Request(int n, ItemId item) =>
        Apply(EventTypes.ItemOpenRequested, new ItemOpenRequested(Id(n), item, LaunchMode.Open, AgentId.From("codex"), "click"), item);

    [Fact]
    public async Task Launches_are_read_by_id_by_item_and_by_repo_newest_first()
    {
        await Item(A.FirstItemId, _demo);
        await Item(_other, RepoName.From("elsewhere"));
        await Request(1, A.FirstItemId);
        await Request(2, A.FirstItemId);
        await Request(3, _other);
        var launches = new LaunchViews(_views.Reader);

        var one = (await launches.Get(Id(1), Token)).Value;
        var missing = (await launches.Get(Id(9), Token)).Value;
        var ofItem = (await launches.ForItem(A.FirstItemId, Token)).Value;
        var ofRepo = (await launches.ForRepo(_demo, Token)).Value;

        Expect.Equal((LaunchState.Requested, _demo), (one!.State, one.Repo!.Value));
        Assert.Null(missing);
        Expect.Sequence([Id(2), Id(1)], ofItem.Select(r => r.Id));
        Expect.Sequence([Id(2), Id(1)], ofRepo.Select(r => r.Id));
    }
}
