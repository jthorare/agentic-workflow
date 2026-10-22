using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Testing.Fixtures;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Tests.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>
/// The map publish gate on a started host (code index spec 6.4, 11 "Integration", owner unit F1P-b2): the real refresh job, publish job and service tick over real temp files, a scripted code index
/// (<see cref="ScriptedOutlooks"/>, whose answer changes between ticks) and the host's fake clock. The refresh draft's deterministic proposals are undecided until a person accepts them at the desk, which is
/// what the publish job's promote needs (SND-CONTEXT-046 otherwise).
/// </summary>
public sealed class MapPublishGateHostTests : IDisposable
{
    private const string Repo = "split-panes";
    private static readonly TimeSpan _wait = TimeSpan.FromMinutes(10);
    private readonly FixtureRepo _repo = FixtureRepo.Build();
    private readonly string _locks = Directory.CreateTempSubdirectory("aw-f1pb2-locks-").FullName;
    private readonly List<string> _homes = [];
    private readonly List<IDisposable> _more = [];

    public void Dispose()
    {
        _repo.Dispose();
        Directory.Delete(_locks, recursive: true);
        _homes.ForEach(h => Directory.Delete(h, recursive: true));
        _more.ForEach(m => m.Dispose());
    }

    // The engine every test runs on lets a test act between the cleanup pass and the promote of a publish (what a writer racing the job does).
    private Func<ContextScope, ValueTask>? _beforePromote;
    private MapPublishRig? _rig;
    private int _runs;

    private async Task<MapPublishRig> Rig(int wait = 10)
    {
        _rig = await MapPublishRig.Start([(Repo, _repo.Path)], wait, _locks, extra: services => RecordingEngine.Install(services, [], beforePromote: scope => _beforePromote?.Invoke(scope) ?? ValueTask.CompletedTask));
        return _rig;
    }

    // A scoping run proposes a surface after the cleanup pass settled the queue: the promote then finds an item only a person can decide (046).
    private void ProposeBeforeEachPromote() => _beforePromote = async _ => await _rig!.Propose(Repo, ++_runs);

    private static string Text(LedgerEvent e, string name) => MapPublishRig.Payload(e).GetProperty(name).GetString()!;

    private static int Number(LedgerEvent e, string name) => MapPublishRig.Payload(e).GetProperty(name).GetInt32();

    private static async Task<int> Started(MapPublishRig rig, string kind) =>
        (await rig.Of(EventTypes.JobStarted)).Count(e => MapPublishRig.Payload(e).GetProperty("kind").GetString() == kind);

    // A hold on a moved trunk: the first map is current, the trunk moves, the index is still coming.
    private static async Task<string> Held(MapPublishRig rig, string repo)
    {
        await rig.FirstMap();
        var moved = await MapPublishRig.Commit(repo);
        rig.Outlooks.All = IndexOutlook.Coming;
        var held = await rig.Tick();
        Assert.True(held.IsSuccess, held.IsError ? held.Error.Message : null);
        Assert.NotNull((await rig.State()).Hold);
        return moved;
    }

    [Fact(Timeout = 240_000), Trait("Lane", "integration")]
    public async Task A_held_version_promotes_when_the_index_lands_between_two_ticks_and_the_new_current_keeps_the_held_heads()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var rig = await Rig();
        var moved = await Held(rig, _repo.Path);
        await rig.AcceptDeterministic();

        var waiting = await rig.Tick();
        rig.Outlooks.All = IndexOutlook.Built;
        var ready = await rig.Tick();

        Assert.Contains("held", waiting.Value.Summary, StringComparison.Ordinal);
        Assert.False(ready.Value.Idle);
        var published = (await rig.Of(EventTypes.ContextMapPublished))[^1];
        var promoted = (await rig.Of(EventTypes.ContextMapPromoted))[^1];
        Assert.True(promoted.Sequence < published.Sequence);
        Assert.Equal(("IndexReady", "check", 0), (Text(published, "reason"), Text(published, "via"), Number(published, "staleRepos")));
        Assert.Equal(moved, MapPublishRig.Payload(published).GetProperty("heads").GetProperty(Repo).GetString());
        var state = await rig.State();
        Assert.Null(state.Hold);
        Assert.Equal(Number(published, "version"), state.Current);
        Assert.Equal(moved, state.CurrentHeads.Single().Commit);
        var refreshed = MapPublishRig.Payload((await rig.Of(EventTypes.ContextMapRefreshed))[^1]);
        Assert.True(refreshed.GetProperty("held").GetBoolean());
    }

    [Fact(Timeout = 240_000), Trait("Lane", "integration")]
    public async Task A_held_version_times_out_at_the_bound_and_publishes_with_the_stale_repo_counted()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var rig = await Rig();
        _ = await Held(rig, _repo.Path);
        await rig.AcceptDeterministic();

        rig.Clock.Advance(_wait - TimeSpan.FromSeconds(1));
        var inside = await rig.Tick();
        rig.Clock.Advance(TimeSpan.FromSeconds(1));
        var due = await rig.Tick();

        Assert.Contains("held", inside.Value.Summary, StringComparison.Ordinal);
        Assert.False(due.Value.Idle);
        var published = (await rig.Of(EventTypes.ContextMapPublished))[^1];
        Assert.Equal(("TimedOut", 1, 1, (int)_wait.TotalSeconds), (Text(published, "reason"), Number(published, "repos"), Number(published, "staleRepos"), Number(published, "waitedSeconds")));
    }

    [Fact(Timeout = 240_000), Trait("Lane", "integration")]
    public async Task A_two_repo_map_with_one_repo_stale_publishes_at_the_bound_naming_one_stale_repo()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        var other = FixtureRepo.Build();
        _more.Add(other);
        await using var rig = await MapPublishRig.Start([("alpha", _repo.Path), ("beta", other.Path)], 10, _locks);
        await rig.FirstMap();
        _ = await MapPublishRig.Commit(_repo.Path);
        rig.Outlooks.All = IndexOutlook.Built;
        rig.Outlooks.ByRepo["beta"] = IndexOutlook.Coming;
        Assert.True((await rig.Tick()).IsSuccess);
        await rig.AcceptDeterministic();

        var waiting = await rig.Tick();
        rig.Clock.Advance(_wait);
        _ = await rig.Tick();

        Assert.Contains("held", waiting.Value.Summary, StringComparison.Ordinal);
        var held = (await rig.Of(EventTypes.ContextMapHeld))[^1];
        Assert.Equal((2, 1), (Number(held, "repos"), Number(held, "waitingOn")));
        var published = (await rig.Of(EventTypes.ContextMapPublished))[^1];
        Assert.Equal(("TimedOut", 2, 1), (Text(published, "reason"), Number(published, "repos"), Number(published, "staleRepos")));
    }

    [Fact(Timeout = 240_000), Trait("Lane", "integration")]
    public async Task A_restart_mid_wait_resumes_with_the_stored_since()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        var home = Directory.CreateTempSubdirectory("aw-f1pb2-home-").FullName;
        _homes.Add(home);
        var outlooks = new ScriptedOutlooks { All = IndexOutlook.Coming };
        DateTimeOffset since;
        await using (var first = await MapPublishRig.Start([(Repo, _repo.Path)], 10, _locks, outlooks, home))
        {
            _ = await Held(first, _repo.Path);
            since = (await first.State()).Hold!.Since;
            await JobHost.ReleaseLease(first.Host, TestContext.Current.CancellationToken);   // the stop released it
        }

        await using var second = await MapPublishRig.Start([(Repo, _repo.Path)], 10, _locks, outlooks, home, idScope: 1, restart: true);
        second.Clock.SetUtcNow(since + TimeSpan.FromMinutes(11));
        var resumed = (await second.State()).Hold!.Since;
        await second.AcceptDeterministic();
        _ = await second.Tick();

        Assert.Equal(since, resumed);
        var published = (await second.Of(EventTypes.ContextMapPublished))[^1];
        Assert.Equal(("TimedOut", 11 * 60), (Text(published, "reason"), Number(published, "waitedSeconds")));
    }

    [Fact(Timeout = 240_000), Trait("Lane", "integration")]
    public async Task A_chain_of_two_moves_publishes_at_most_the_wait_after_the_first_hold()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var rig = await Rig();
        _ = await Held(rig, _repo.Path);
        var first = (await rig.State()).Hold!.Since;
        rig.Clock.Advance(TimeSpan.FromMinutes(4));
        var second = await MapPublishRig.Commit(_repo.Path);

        var rebuilt = await rig.Tick();
        var hold = (await rig.State()).Hold!;
        await rig.AcceptDeterministic();
        rig.Clock.Advance(TimeSpan.FromMinutes(6));
        _ = await rig.Tick();

        Assert.False(rebuilt.Value.Idle);
        Assert.Equal(first, hold.Since);   // the second hold keeps the chain's start
        var holds = await rig.Of(EventTypes.ContextMapHeld);
        Assert.Equal([false, true], holds.Select(e => MapPublishRig.Payload(e).GetProperty("chained").GetBoolean()));
        var published = (await rig.Of(EventTypes.ContextMapPublished))[^1];
        Assert.Equal(("TimedOut", (int)_wait.TotalSeconds), (Text(published, "reason"), Number(published, "waitedSeconds")));
        Assert.Equal(second, MapPublishRig.Payload(published).GetProperty("heads").GetProperty(Repo).GetString());
    }

    [Fact(Timeout = 240_000), Trait("Lane", "integration")]
    public async Task A_human_promote_clears_the_hold_and_the_chain_so_the_next_wait_starts_fresh()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var rig = await Rig();
        _ = await Held(rig, _repo.Path);
        rig.Clock.Advance(TimeSpan.FromMinutes(5));
        await rig.AcceptDeterministic();

        await rig.HumanPromote();
        var cleared = await rig.State();
        _ = await MapPublishRig.Commit(_repo.Path);
        rig.Clock.Advance(TimeSpan.FromMinutes(2));
        _ = await rig.Tick();

        Assert.Null(cleared.Hold);
        var again = await rig.State();
        Assert.Equal(rig.Clock.GetUtcNow(), again.Hold!.Since);   // not the first chain's start, which a promote clears
        var holds = await rig.Of(EventTypes.ContextMapHeld);
        Assert.False(MapPublishRig.Payload(holds[^1]).GetProperty("chained").GetBoolean());
    }

    [Fact(Timeout = 240_000), Trait("Lane", "integration")]
    public async Task A_human_build_drops_the_hold_and_the_next_tick_rebuilds_only_while_the_map_is_behind_trunk()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var rig = await Rig();
        var moved = await Held(rig, _repo.Path);
        var jobs = await Started(rig, "RefreshContextMap");

        Assert.True((await rig.Build()).IsSuccess);
        var dropped = await rig.State();
        var behind = await rig.Tick();

        Assert.Null(dropped.Hold);
        Assert.False(behind.Value.Idle);   // the current map is at the old head: behind trunk
        Assert.Equal(jobs + 1, await Started(rig, "RefreshContextMap"));

        await rig.AcceptDeterministic();
        await rig.HumanPromote();
        var settled = await rig.Tick();

        var state = await rig.State();
        Assert.Null(state.Hold);
        Assert.Equal(moved, state.CurrentHeads.Single().Commit);
        Assert.True(settled.Value.Idle);   // the current map is at trunk: nothing to rebuild
        Assert.Equal(jobs + 1, await Started(rig, "RefreshContextMap"));
    }

    [Fact(Timeout = 240_000), Trait("Lane", "integration")]
    public async Task A_refresh_replacing_the_draft_before_the_promote_is_190_the_hold_is_dropped_and_the_new_draft_is_not_promoted()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var rig = await Rig();
        var replaced = false;
        var armed = false;
        _beforePromote = async scope =>
        {
            if (!armed || replaced)
            {
                return;
            }

            replaced = true;   // a refresh built a new draft in the gap
            var engine = rig.Host.App.Services.GetRequiredService<IContextEngine>();
            Assert.True((await engine.BuildMap(progress: null, scope, TestContext.Current.CancellationToken)).IsSuccess);
        };
        _ = await Held(rig, _repo.Path);
        await rig.AcceptDeterministic();
        var current = (await rig.State()).Current;
        rig.Outlooks.All = IndexOutlook.Built;
        armed = true;

        var tick = await rig.Tick();

        Assert.True(replaced);
        Assert.False(tick.Value.Idle);
        var state = await rig.State();
        Assert.Equal((null, current), (state.Hold, state.Current));   // the new draft was not promoted
        var dropped = (await rig.Of(EventTypes.ContextMapHoldDropped))[^1];
        Assert.Equal("Gone", Text(dropped, "reason"));
        Assert.Empty(await rig.Of(EventTypes.ContextMapPublished));   // no map went ahead; the first one was a person's
        var finished = MapPublishRig.Payload((await rig.Of(EventTypes.JobFinished))[^1]);
        Assert.Equal(("succeeded", ContextErrors.DraftChanged.Code), (finished.GetProperty("outcome").GetString(), finished.GetProperty("result").GetProperty("code").GetString()));
    }

    [Fact(Timeout = 240_000), Trait("Lane", "integration")]
    public async Task A_held_draft_with_an_undecided_proposal_is_not_promoted_the_job_fails_the_hold_stays_backoff_applies_and_the_alarm_counts()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var rig = await Rig();
        _ = await Held(rig, _repo.Path);
        ProposeBeforeEachPromote();
        rig.Clock.Advance(TimeSpan.FromSeconds(1));   // the publish ends after the refresh did, as it does in time
        rig.Outlooks.All = IndexOutlook.Built;

        var first = await rig.Tick();
        var inBackoff = await rig.Tick();
        var publishes = await Started(rig, "PublishContextMap");
        rig.Clock.Advance(TimeSpan.FromMinutes(6));
        _ = await rig.Tick();
        rig.Clock.Advance(TimeSpan.FromMinutes(11));
        _ = await rig.Tick();
        rig.Clock.Advance(TimeSpan.FromMinutes(1));
        var alarm = await rig.Tick();

        Assert.False(first.Value.Idle);
        Assert.Equal(1, publishes);
        Assert.True((await rig.State()).Hold is not null, await Dump(rig));   // still held, not promoted
        Assert.Empty(await rig.Of(EventTypes.ContextMapPublished));
        Assert.Equal(3, await Started(rig, "PublishContextMap"));
        Assert.True(alarm.IsError);
        Assert.Contains("SND-CONTEXT-046", alarm.Error.Message, StringComparison.Ordinal);
    }

    [Fact(Timeout = 240_000), Trait("Lane", "integration")]
    public async Task A_publish_in_its_backoff_lets_the_move_check_and_the_alarm_run()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var rig = await Rig();
        _ = await Held(rig, _repo.Path);
        ProposeBeforeEachPromote();
        rig.Clock.Advance(TimeSpan.FromSeconds(1));   // the publish ends after the refresh did, as it does in time
        rig.Outlooks.All = IndexOutlook.Built;
        _ = await rig.Tick();
        rig.Clock.Advance(TimeSpan.FromMinutes(6));
        _ = await rig.Tick();
        rig.Clock.Advance(TimeSpan.FromMinutes(11));
        _ = await rig.Tick();
        rig.Clock.Advance(TimeSpan.FromMinutes(1));
        var refreshes = await Started(rig, "RefreshContextMap");
        var publishes = await Started(rig, "PublishContextMap");
        rig.Outlooks.All = IndexOutlook.Coming;
        _ = await MapPublishRig.Commit(_repo.Path);   // the trunk moves while the third publish failure's backoff holds

        var tick = await rig.Tick();

        Assert.True(tick.IsError);   // the alarm
        Assert.Contains("refresh", tick.Error.Message, StringComparison.Ordinal);
        Assert.Equal(publishes, await Started(rig, "PublishContextMap"));   // the backoff held the publish
        Assert.Equal(refreshes + 1, await Started(rig, "RefreshContextMap"));   // the move check ran
    }

    [Fact(Timeout = 240_000), Trait("Lane", "integration")]
    public async Task The_map_events_hold_only_enum_reasons_counts_version_numbers_and_profile_repo_names()
    {
        TestContext.Current.CancellationToken.ThrowIfCancellationRequested();
        await using var rig = await Rig();
        _ = await Held(rig, _repo.Path);
        await rig.AcceptDeterministic();
        rig.Clock.Advance(_wait);
        _ = await rig.Tick();

        var held = await rig.Of(EventTypes.ContextMapHeld);
        var published = await rig.Of(EventTypes.ContextMapPublished);
        var refreshed = (await rig.Of(EventTypes.ContextMapRefreshed))[^1];

        Assert.Equal("chained repo repos version waitMinutes waitingOn", string.Join(' ', Names(held[0])));
        Assert.Equal("anyStale heads reason repo repos staleRepos version via waitedSeconds", string.Join(' ', Names(published[^1])));
        Assert.Contains("held", Names(refreshed), StringComparer.Ordinal);
        Assert.Equal(Repo, Text(held[0], "repo"));
        Assert.Equal(Repo, Assert.Single(MapPublishRig.Payload(published[^1]).GetProperty("heads").EnumerateObject()).Name);
        foreach (var e in held.Concat(published))
        {
            Assert.DoesNotContain(_repo.Path, e.Envelope.Payload, StringComparison.Ordinal);
            Assert.DoesNotContain("SND-", e.Envelope.Payload, StringComparison.Ordinal);
        }
    }

    private static async Task<string> Dump(MapPublishRig rig) =>
        string.Join('\n', (await rig.Events()).Where(e => e.Envelope.Type.StartsWith("job.", StringComparison.Ordinal) || e.Envelope.Type.StartsWith("context.map", StringComparison.Ordinal))
            .Select(e => e.Envelope.Type + " " + e.Envelope.Payload));

    private static string[] Names(LedgerEvent e) => [.. MapPublishRig.Payload(e).EnumerateObject().Select(static p => p.Name).Order(StringComparer.Ordinal)];
}
