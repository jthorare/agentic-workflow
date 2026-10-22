using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Map;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The refresh job's work (owner F1, 2026-10-19): build, clean up, promote through the engine and record the three events; any step's error fails the job with the later steps not run.</summary>
public sealed class RefreshContextMapServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
    private readonly SliceHarness _h = new();
    private readonly RefreshEngineProxy _engine = RefreshEngineProxy.Create();
    private readonly FakeJobContextAccessor _jobs = new();
    private readonly ScriptedOutlooks _outlooks = new();
    private readonly List<string> _progress = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Kernel.Result<RefreshContextMapResult>> Run()
    {
        _h.Clock.SetUtcNow(_now);
        _ = await _h.HoldLease();
        _ = await _h.Profiles.Save(SliceHarness.Profile(), Ct);
        var service = new RefreshContextMapService(new ContextScopes(_h.Profiles, _h.Callers), _engine.Proxy, _outlooks, new MapEventSource(_h.Held, _h.Store, _h.Events), _jobs, _h.Clock);
        using var job = _jobs.Enter(new JobContext(JobId.New(_h.Ids), JobKind.From("RefreshContextMap"), new Progress(_progress)));
        using var caller = _h.Callers.EnterService("context-map-refresh");
        return await service.Handle(new RefreshContextMapRequest(), Ct);
    }

    private string[] Types() => [.. _h.Store.Events.Select(static e => e.Envelope.Type)];

    private sealed class Progress(List<string> lines) : IProgress<string>
    {
        public void Report(string value) => lines.Add(value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Refresh_builds_cleans_up_and_promotes_and_records_built_promoted_refreshed_in_order()
    {
        var result = await Run();

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Code : null);
        Assert.Equal(["MapHeads", "BuildMap", "ReviewQueue", "CleanDraft", "Held", "Promote"], _engine.Log);
        Assert.Equal([EventTypes.ContextMapBuilt, EventTypes.ContextMapPromoted, EventTypes.ContextMapPublished, EventTypes.ContextMapRefreshed], Types());
        var refreshed = result.Value;
        Assert.Equal(("app", 2, 5, 2, 1, "agent"), (refreshed.Repo, refreshed.Version, refreshed.Accepted, refreshed.Removed, refreshed.Dropped, refreshed.Cleanup));
        Assert.Equal(new Dictionary<string, string>(StringComparer.Ordinal) { ["app"] = "abc123" }, refreshed.Heads);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_refreshed_event_carries_the_version_the_heads_and_the_cleanup_counts()
    {
        _ = await Run();

        using var payload = JsonDocument.Parse(_h.Store.Events.Single(static e => e.Envelope.Type == EventTypes.ContextMapRefreshed).Envelope.Payload);
        var root = payload.RootElement;
        Assert.Equal(("app", 2, "abc123", 5, 2, 1, "agent"),
            (root.GetProperty("repo").GetString(), root.GetProperty("version").GetInt32(), root.GetProperty("heads").GetProperty("app").GetString(),
             root.GetProperty("accepted").GetInt32(), root.GetProperty("removed").GetInt32(), root.GetProperty("dropped").GetInt32(), root.GetProperty("cleanup").GetString()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Progress_names_the_three_phases_with_the_number_left_to_decide()
    {
        _ = await Run();

        Assert.Equal(["building", "cleaning up: 3 to decide", "promoting"], _progress);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_error_fails_the_job_with_nothing_cleaned_or_promoted()
    {
        _engine.Built = ContextErrors.HeavyLockBusy.With("busy");

        var result = await Run();

        Assert.Equal(ContextErrors.HeavyLockBusy.Code, result.Error.Code);
        Assert.Equal(["MapHeads", "BuildMap"], _engine.Log);
        Assert.Empty(Types());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cleanup_error_leaves_the_built_draft_unpromoted()
    {
        _engine.Cleaned = ContextErrors.SessionsDisabled.With("refused");

        var result = await Run();

        Assert.Equal(ContextErrors.SessionsDisabled.Code, result.Error.Code);
        Assert.DoesNotContain("Promote", _engine.Log, StringComparer.Ordinal);
        Assert.Equal([EventTypes.ContextMapBuilt], Types());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_promote_refusal_046_fails_the_job_and_records_no_promotion()
    {
        _engine.Promoted = ContextErrors.PromoteRefused.With("draft v2 has 1 undecided review items");

        var result = await Run();

        Assert.Equal("SND-CONTEXT-046", result.Error.Code);
        Assert.Equal([EventTypes.ContextMapBuilt], Types());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_heads_error_fails_the_job_before_the_build()
    {
        _engine.Heads = ContextErrors.RepoOutsideScope.With("outside");

        var result = await Run();

        Assert.True(result.IsError);
        Assert.Equal(["MapHeads"], _engine.Log);
    }
    private JsonElement Payload(string type)
    {
        using var document = JsonDocument.Parse(_h.Store.Events.Single(e => e.Envelope.Type == type).Envelope.Payload);
        return document.RootElement.Clone();
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_refresh_that_promotes_asks_for_the_draft_it_decided_about_and_records_the_published_reason()
    {
        var result = await Run();

        Assert.Equal([2, 2], _engine.Expected);
        Assert.False(result.Value.Held);
        var published = Payload(EventTypes.ContextMapPublished);
        Assert.Equal(("app", 2, "NoIndex", "refresh", 1, 0, 0, "abc123"),
            (published.GetProperty("repo").GetString(), published.GetProperty("version").GetInt32(), published.GetProperty("reason").GetString(), published.GetProperty("via").GetString(),
             published.GetProperty("repos").GetInt32(), published.GetProperty("staleRepos").GetInt32(), published.GetProperty("waitedSeconds").GetInt32(), published.GetProperty("heads").GetProperty("app").GetString()));
        Assert.False(Payload(EventTypes.ContextMapRefreshed).GetProperty("held").GetBoolean());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_built_index_publishes_at_once_with_index_ready()
    {
        _outlooks.All = IndexOutlook.Built;

        var result = await Run();

        Assert.False(result.Value.Held);
        Assert.Equal("IndexReady", Payload(EventTypes.ContextMapPublished).GetProperty("reason").GetString());
        Assert.Empty(_engine.Holds);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_index_still_coming_holds_the_draft_after_the_cleanup_and_never_promotes()
    {
        _outlooks.All = IndexOutlook.Coming;

        var result = await Run();

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Code : null);
        Assert.Equal(["MapHeads", "BuildMap", "ReviewQueue", "CleanDraft", "Held", "Hold"], _engine.Log);
        Assert.Equal([EventTypes.ContextMapBuilt, EventTypes.ContextMapHeld, EventTypes.ContextMapRefreshed], Types());
        Assert.True(result.Value.Held);
        Assert.Equal(2, result.Value.Version);
        var hold = Assert.Single(_engine.Holds);
        Assert.Equal((2, _now, "app", "abc123", true), (hold.Version, hold.Since, hold.Heads[0].Repo.Value, hold.Heads[0].Commit, hold.Heads[0].OnDefault));
        Assert.True(Payload(EventTypes.ContextMapRefreshed).GetProperty("held").GetBoolean());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_held_event_counts_the_repos_what_it_waits_on_and_the_bound()
    {
        _outlooks.All = IndexOutlook.Coming;

        _ = await Run();

        var held = Payload(EventTypes.ContextMapHeld);
        Assert.Equal(("app", 2, 1, 1, 15, false), (held.GetProperty("repo").GetString(), held.GetProperty("version").GetInt32(), held.GetProperty("repos").GetInt32(), held.GetProperty("waitingOn").GetInt32(), held.GetProperty("waitMinutes").GetInt32(), held.GetProperty("chained").GetBoolean()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_wait_that_began_with_an_earlier_hold_is_chained_and_does_not_restart()
    {
        _outlooks.All = IndexOutlook.Coming;
        _engine.Chain = _now - TimeSpan.FromMinutes(5);

        _ = await Run();

        Assert.True(Payload(EventTypes.ContextMapHeld).GetProperty("chained").GetBoolean());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_wait_that_began_with_an_earlier_hold_and_is_over_promotes_as_timed_out_naming_the_stale_repo()
    {
        _outlooks.All = IndexOutlook.Coming;
        _engine.Chain = _now - TimeSpan.FromMinutes(20);

        var result = await Run();

        Assert.False(result.Value.Held);
        Assert.Equal(["MapHeads", "BuildMap", "ReviewQueue", "CleanDraft", "Held", "Hold", "Promote"], _engine.Log);
        var published = Payload(EventTypes.ContextMapPublished);
        Assert.Equal(("TimedOut", 1, 1200, true), (published.GetProperty("reason").GetString(), published.GetProperty("staleRepos").GetInt32(), published.GetProperty("waitedSeconds").GetInt32(), published.GetProperty("anyStale").GetBoolean()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_first_map_never_waits()
    {
        _outlooks.All = IndexOutlook.Coming;
        _engine.State = new MapPublishState(Hold: null, LatestDraft: 2, Current: null, CurrentHeads: []);

        _ = await Run();

        Assert.Equal("FirstMap", Payload(EventTypes.ContextMapPublished).GetProperty("reason").GetString());
        Assert.Empty(_engine.Holds);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_broken_index_publishes_at_once()
    {
        _outlooks.All = IndexOutlook.Broken;

        _ = await Run();

        Assert.Equal("IndexBroken", Payload(EventTypes.ContextMapPublished).GetProperty("reason").GetString());
        Assert.Empty(_engine.Holds);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_hold_the_store_refuses_fails_the_job_and_a_replaced_draft_is_190()
    {
        _outlooks.All = IndexOutlook.Coming;
        _engine.HoldFailure = ContextErrors.DraftChanged.With("v3 replaced it");

        var result = await Run();

        Assert.Equal("SND-CONTEXT-190", result.Error.Code);
        Assert.DoesNotContain("Promote", _engine.Log, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_outlooks_read_error_fails_the_job_with_the_draft_unpromoted()
    {
        _outlooks.Failure = SindriErrors.ServiceFailed.With("index unreadable");

        var result = await Run();

        Assert.Equal(SindriErrors.ServiceFailed.Code, result.Error.Code);
        Assert.DoesNotContain("Promote", _engine.Log, StringComparer.Ordinal);
    }
}
