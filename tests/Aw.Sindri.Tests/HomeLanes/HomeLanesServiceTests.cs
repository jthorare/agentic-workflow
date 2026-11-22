using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Home;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.HomeCollect;
using Aw.Sindri.Features.HomeLanes;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.HomeLanes;

/// <summary>
/// The 'home-lanes' collector (home spec §3.2, §3.3, E1): a lane at its mark, a limited subject and each merge-queue entry raise a row; a lane under the mark again, a PR
/// gone from the queue clear theirs; a box with no aw-meta team is an ok pass that writes nothing and keeps what it raised.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class HomeLanesServiceTests
{
    private const string RepoDirectory = "/home/owner/repos/agentic-workflow";

    private readonly SliceHarness _h = new();
    private readonly FakeMetaTeamReader _team;
    private readonly FakeForgeRepos _repos = new FakeForgeRepos().Put(RepoDirectory, new PullRepo("github.com", "jthorare", "agentic-workflow"));
    private readonly InMemoryHomeView _view = new();
    private readonly HomeLanesService _service;
    private LeaseGrant? _grant;
    private long _projected;

    public HomeLanesServiceTests()
    {
        _team = new FakeMetaTeamReader(Box(recycle: true, queue: [3099]));
        _service = new HomeLanesService(_team, _repos, _view, new HomeRowWriter(_h.Store, _h.Events), new PassThroughScrubber(), _h.Clock);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_lane_at_its_mark_and_a_queued_pr_raise_rows_and_a_quiet_pass_writes_nothing()
    {
        var first = await Tick();
        var second = await Tick();

        Assert.Equal(["lane:recycle:meta", "gh:ready:jthorare/agentic-workflow#3099"], (await Raised()).Select(static r => r.Row.Id.Value).ToArray());
        Assert.False(first.Idle);
        Assert.True(second.Idle);
    }

    [Fact]
    public async Task A_lane_under_the_mark_again_and_a_pr_gone_from_the_queue_clear_their_rows()
    {
        _ = await Tick();
        _team.Team = Box(recycle: false, queue: []);

        _ = await Tick();

        Assert.Equal(
            [EventTypes.HomeRowRaised, EventTypes.HomeRowRaised, EventTypes.HomeRowCleared, EventTypes.HomeRowCleared],
            (await Events()).Select(static e => e.Envelope.Type).ToArray());
        Assert.Null((await _view.Get(HomeRowId.From("lane:recycle:meta"), Token)).Value);
    }

    [Fact]
    public async Task The_next_recycle_mark_after_a_clear_is_a_new_row()
    {
        _ = await Tick();
        _team.Team = Box(recycle: false, queue: [3099]);
        _ = await Tick();
        _team.Team = Box(recycle: true, queue: [3099]);

        _ = await Tick();

        Assert.Equal(2, (await Raised()).Count(static r => r.Row.Id.Value == "lane:recycle:meta"));
        Assert.NotNull((await _view.Get(HomeRowId.From("lane:recycle:meta"), Token)).Value);
    }

    [Fact]
    public async Task Ten_thousand_more_tokens_change_the_row_and_a_few_more_do_not()
    {
        _ = await Tick();
        _team.Team = Box(recycle: true, queue: [3099], tokens: 209_000);
        var few = await Tick();
        _team.Team = Box(recycle: true, queue: [3099], tokens: 212_000);

        _ = await Tick();

        Assert.True(few.Idle);
        var changed = Assert.Single(await Events(), static e => e.Envelope.Type == EventTypes.HomeRowChanged);
        var payload = _h.Events.Payload<HomeRowChanged>(changed).Value;
        Assert.Equal((2L, "meta: at 212k, recycle from a digest"), (payload.Version, payload.Title));
    }

    [Fact]
    public async Task A_box_with_no_team_is_an_ok_pass_that_keeps_the_rows_it_raised()
    {
        _ = await Tick();
        _team.Team = MetaTeamRead.NotAvailable("aw-meta is not on the PATH");

        var pass = await Tick();

        Assert.True(pass.Idle);
        Assert.StartsWith("ok: lanes-unavailable: aw-meta is not on the PATH", pass.Summary, StringComparison.Ordinal);
        Assert.Equal(2, (await Events()).Count);
        Assert.NotNull((await _view.Get(HomeRowId.From("lane:recycle:meta"), Token)).Value);
    }

    [Fact]
    public async Task A_repository_the_forge_port_cannot_name_keys_queue_rows_by_number()
    {
        _team.Team = Box(recycle: false, queue: [3099]) with { RepoDirectory = "/somewhere/else" };

        _ = await Tick();

        Assert.Equal("lane:ready:3099", Assert.Single(await Raised()).Row.Id.Value);
    }

    [Fact]
    public async Task Rows_of_other_sources_are_never_cleared()
    {
        _view.Put(HomeRowSamples.Row("gh:pr:jthorare/agentic-workflow#3120"));
        _team.Team = Box(recycle: false, queue: []);

        var pass = await Tick();

        Assert.True(pass.Idle);
        Assert.Empty(await Events());
    }

    private static MetaTeamRead Box(bool recycle, int[] queue, long tokens = 205_000) => new(
        200_000,
        [new MetaLane("meta", "sindri-v3-meta", "6f1c2a9e", recycle ? tokens : 120_000, 200_000, recycle ? "Recycle" : "Ok")],
        [.. queue.Select(static pr => new MetaQueuedPr(pr, "sindri-v3-meta", "Pass", Github: null))],
        [new MetaUsage("meta", "Ok", Kind: null, ResetsAt: null, "claude-code")],
        RepoDirectory,
        Unavailable: null);

    private async Task<LeaseGrant> Grant() => _grant ??= await _h.HoldLease();

    // One pass a minute after the last, then the projector's fold of what it wrote.
    private async Task<ScheduledResult> Tick()
    {
        var grant = await Grant();
        _h.Clock.Advance(TimeSpan.FromMinutes(1));
        var result = await _service.Run(new ScheduledRun(grant, _h.Clock.GetUtcNow()), Token);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.Message : null);
        foreach (var recorded in await Events(after: _projected))
        {
            await Fold(recorded);
            _projected = recorded.Sequence;
        }

        return result.Value;
    }

    private async Task Fold(LedgerEvent recorded)
    {
        if (recorded.Envelope.Type == EventTypes.HomeRowRaised)
        {
            _view.Put(_h.Events.Payload<HomeRowRaised>(recorded).Value.Row);
        }
        else if (recorded.Envelope.Type == EventTypes.HomeRowChanged)
        {
            var p = _h.Events.Payload<HomeRowChanged>(recorded).Value;
            var row = (await _view.Get(p.Id, Token)).Value!;   // why: a change follows the raise of its row, which this fold put in the view
            _view.Put(row with { Version = p.Version, Title = p.Title ?? row.Title, Facts = p.Facts ?? row.Facts, WaitsOn = p.WaitsOn ?? row.WaitsOn, Actions = p.Actions ?? row.Actions, Links = p.Links ?? row.Links });
        }
        else if (recorded.Envelope.Type == EventTypes.HomeRowCleared)
        {
            _view.Remove(_h.Events.Payload<HomeRowCleared>(recorded).Value.Id);
        }
    }

    private async Task<IReadOnlyList<HomeRowRaised>> Raised() =>
        [.. (await Events()).Where(static e => e.Envelope.Type == EventTypes.HomeRowRaised).Select(e => _h.Events.Payload<HomeRowRaised>(e).Value)];

    private async Task<IReadOnlyList<LedgerEvent>> Events(long after = 0) =>
        [.. (await _h.Store.ReadAfter(after, 1000, Token)).Value.Where(static e => e.Envelope.Type.StartsWith("home.", StringComparison.Ordinal))];
}
