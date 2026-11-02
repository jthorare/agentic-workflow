using System.Text.Json;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>Concept pins spec §3.7: the rebuild state Context's stale-map note and the concept card read.</summary>
public sealed class MapRebuildStateTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
    private readonly SliceHarness _h = new();
    private readonly InMemoryJobViews _jobs = new();
    private readonly HeadsEngineProxy _engine = HeadsEngineProxy.Create();
    private int _next;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<MapRebuildState> State()
    {
        _h.Clock.SetUtcNow(_now);
        var loaded = SliceHarness.Profile("5s");
        loaded = loaded with { Profile = loaded.Profile with { Context = JsonDocument.Parse("""{"docs":{"globs":["docs/**/*.md"]}}""").RootElement.Clone() } };
        _ = await _h.Profiles.Save(loaded, Ct);
        return new MapRebuildState(new ContextScopes(_h.Profiles, _h.Callers), () => _engine.Proxy, _jobs, _h.Clock);
    }

    private void Refresh(JobState state, TimeSpan endedAgo, params string[] lines)
    {
        _next++;
        _jobs.Put(new JobRow(JobId.New(_h.Ids), JobKind.From("RefreshContextMap"), JobKey.From("refresh"), Item: null, Repo: null, Service: null, state,
            "service:context-map-refresh", _now - endedAgo - TimeSpan.FromSeconds(5), state is JobState.Running or JobState.Waiting ? null : _now - endedAgo,
            [.. lines.Select((l, i) => new JobLine(i + 1, l, _now))], Rounds: lines.Length, Summary: null, state == JobState.Failed ? "SND-CONTEXT-030" : null, Result: null, CancelledBy: null, Sequence: _next));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_running_refresh_is_building_with_its_latest_progress_line()
    {
        var state = await State();
        Refresh(JobState.Running, TimeSpan.Zero, "resolving calls", "extracting 4,200 of 9,111 files");

        var rebuild = await state.Read(Ct);

        Assert.Equal(MapRebuildPhase.Building, rebuild.Phase);
        Assert.Equal("extracting 4,200 of 9,111 files", rebuild.Progress);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_held_map_says_why_no_refresh_starts()
    {
        var state = await State();
        _engine.Heads = [new RepoHead(ContextRepoName.From("a"), "1", OnDefault: true)];
        _engine.State = new MapPublishState(new MapHold(2, _now, [new MapHead(ContextRepoName.From("a"), "1", OnDefault: true)]), LatestDraft: 2, Current: 1, CurrentHeads: []);

        Assert.Equal(MapRebuildPhase.MapHeld, (await state.Read(Ct)).Phase);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_repo_on_its_main_branch_is_told_before_the_backoff()
    {
        var state = await State();
        _engine.Heads = [new RepoHead(ContextRepoName.From("a"), "1", OnDefault: false)];
        Refresh(JobState.Failed, TimeSpan.FromMinutes(1));

        Assert.Equal(MapRebuildPhase.NoMainBranch, (await state.Read(Ct)).Phase);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_refresh_backs_off_until_the_retry_time_and_then_is_idle()
    {
        var state = await State();
        _engine.Heads = [new RepoHead(ContextRepoName.From("a"), "1", OnDefault: true)];
        Refresh(JobState.Failed, TimeSpan.FromMinutes(1));

        var backoff = await state.Read(Ct);
        Assert.Equal(MapRebuildPhase.Backoff, backoff.Phase);
        Assert.Equal(_now - TimeSpan.FromMinutes(1) + TimeSpan.FromMinutes(5), backoff.RetryAt);

        _h.Clock.SetUtcNow(_now + TimeSpan.FromMinutes(10));
        Assert.Equal(MapRebuildPhase.Idle, (await state.Read(Ct)).Phase);
    }
}
