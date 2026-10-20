using System.Text.Json;
using Aw.Context.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The refresh job's work (owner F1, 2026-10-19): build, clean up, promote through the engine and record the three events; any step's error fails the job with the later steps not run.</summary>
public sealed class RefreshContextMapServiceTests
{
    private readonly SliceHarness _h = new();
    private readonly RefreshEngineProxy _engine = RefreshEngineProxy.Create();
    private readonly FakeJobContextAccessor _jobs = new();
    private readonly List<string> _progress = [];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Kernel.Result<RefreshContextMapResult>> Run()
    {
        _ = await _h.HoldLease();
        _ = await _h.Profiles.Save(SliceHarness.Profile(), Ct);
        var service = new RefreshContextMapService(new ContextScopes(_h.Profiles, _h.Callers), _engine.Proxy, _h.Held, _h.Store, _h.Events, _jobs, _h.Clock);
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
        Assert.Equal(["MapHeads", "BuildMap", "ReviewQueue", "CleanDraft", "Promote"], _engine.Log);
        Assert.Equal([EventTypes.ContextMapBuilt, EventTypes.ContextMapPromoted, EventTypes.ContextMapRefreshed], Types());
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
}
