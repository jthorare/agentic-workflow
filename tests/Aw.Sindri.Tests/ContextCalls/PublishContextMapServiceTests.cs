using System.Text.Json;
using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The publish job's work (code index spec 6.4): it asks the decision again, then promotes the held draft, drops the hold, or does nothing; 190 ends it quietly and 046 fails it with the hold kept.</summary>
public sealed class PublishContextMapServiceTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
    private readonly SliceHarness _h = new();
    private readonly RefreshEngineProxy _engine = RefreshEngineProxy.Create();
    private readonly ScriptedOutlooks _outlooks = new();
    private readonly FakeJobContextAccessor _jobs = new();

    public PublishContextMapServiceTests()
    {
        Hold(since: _now - TimeSpan.FromMinutes(20), latestDraft: 2, current: 1);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private void Hold(DateTimeOffset since, int? latestDraft, int? current, int version = 2) =>
        _engine.State = new MapPublishState(new MapHold(version, since, [new MapHead(ContextRepoName.From("app"), "abc123", OnDefault: true)]), latestDraft, current, CurrentHeads: []);

    private async Task<Kernel.Result<PublishContextMapResult>> Run()
    {
        _h.Clock.SetUtcNow(_now);
        _ = await _h.HoldLease();
        _ = await _h.Profiles.Save(SliceHarness.Profile(), Ct);
        var service = new PublishContextMapService(new ContextScopes(_h.Profiles, _h.Callers), _engine.Proxy, _outlooks, new MapEventSource(_h.Held, _h.Store, _h.Events), _jobs, _h.Clock);
        using var job = _jobs.Enter(new JobContext(JobId.New(_h.Ids), JobKind.From("PublishContextMap"), new Progress()));
        using var caller = _h.Callers.EnterService("context-map-refresh");
        return await service.Handle(new PublishContextMapRequest(), Ct);
    }

    private string[] Types() => [.. _h.Store.Events.Select(static e => e.Envelope.Type)];

    private JsonElement Payload(string type)
    {
        using var document = JsonDocument.Parse(_h.Store.Events.Single(e => e.Envelope.Type == type).Envelope.Payload);
        return document.RootElement.Clone();
    }

    private sealed class Progress : IProgress<string>
    {
        public void Report(string value)
        {
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_publish_decision_runs_the_cleanup_pass_promotes_and_appends_promoted_then_published()
    {
        _outlooks.All = IndexOutlook.Built;

        var result = await Run();

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Code : null);
        Assert.Equal(["Held", "CleanDraft", "Promote"], _engine.Log);
        Assert.Equal([2, 2], _engine.Expected);
        Assert.Equal([EventTypes.ContextMapPromoted, EventTypes.ContextMapPublished], Types());
        Assert.Equal((2, "IndexReady", null), (result.Value.Version, result.Value.Reason, result.Value.Code));
        var published = Payload(EventTypes.ContextMapPublished);
        Assert.Equal(("check", 1200, 0), (published.GetProperty("via").GetString(), published.GetProperty("waitedSeconds").GetInt32(), published.GetProperty("staleRepos").GetInt32()));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_wait_that_ran_out_publishes_as_timed_out_with_the_stale_repo_counted()
    {
        _outlooks.All = IndexOutlook.Coming;

        var result = await Run();

        Assert.Equal("TimedOut", result.Value.Reason);
        Assert.Equal(1, Payload(EventTypes.ContextMapPublished).GetProperty("staleRepos").GetInt32());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_decision_that_says_wait_after_all_does_nothing()
    {
        Hold(since: _now - TimeSpan.FromMinutes(1), latestDraft: 2, current: 1);
        _outlooks.All = IndexOutlook.Coming;

        var result = await Run();

        Assert.Equal((2, "Waiting"), (result.Value.Version, result.Value.Reason));
        Assert.Equal(["Held"], _engine.Log);
        Assert.Empty(Types());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_drop_decision_releases_the_hold_and_appends_hold_dropped()
    {
        Hold(since: _now - TimeSpan.FromMinutes(1), latestDraft: 3, current: 1);

        var result = await Run();

        Assert.Equal((2, "Gone", null), (result.Value.Version, result.Value.Reason, result.Value.Code));
        Assert.Equal([2], _engine.Released);
        Assert.Equal([EventTypes.ContextMapHoldDropped], Types());
        var dropped = Payload(EventTypes.ContextMapHoldDropped);
        Assert.Equal((2, "Gone"), (dropped.GetProperty("version").GetInt32(), dropped.GetProperty("reason").GetString()));
        Assert.DoesNotContain("Promote", _engine.Log, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_promote_refused_with_190_ends_succeeded_with_the_code_drops_the_hold_and_raises_nothing()
    {
        _outlooks.All = IndexOutlook.Built;
        _engine.Promoted = ContextErrors.DraftChanged.With("a rebuild replaced it");

        var result = await Run();

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Code : null);
        Assert.Equal((2, "Gone", "SND-CONTEXT-190"), (result.Value.Version, result.Value.Reason, result.Value.Code));
        Assert.Equal([2], _engine.Released);
        Assert.Equal([EventTypes.ContextMapHoldDropped], Types());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_cleanup_refused_with_190_ends_the_same_way()
    {
        _outlooks.All = IndexOutlook.Built;
        _engine.Cleaned = ContextErrors.DraftChanged.With("a rebuild replaced it");

        var result = await Run();

        Assert.Equal("SND-CONTEXT-190", result.Value.Code);
        Assert.DoesNotContain("Promote", _engine.Log, StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_promote_refused_with_046_fails_the_job_and_keeps_the_hold()
    {
        _outlooks.All = IndexOutlook.Built;
        _engine.Promoted = ContextErrors.PromoteRefused.With("draft v2 has 1 undecided review items");

        var result = await Run();

        Assert.Equal("SND-CONTEXT-046", result.Error.Code);
        Assert.Empty(_engine.Released);
        Assert.Empty(Types());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task No_hold_is_a_quiet_success()
    {
        _engine.State = new MapPublishState(Hold: null, LatestDraft: 2, Current: 1, CurrentHeads: []);

        var result = await Run();

        Assert.Equal((0, "NotHeld"), (result.Value.Version, result.Value.Reason));
        Assert.Equal(["Held"], _engine.Log);
    }
}
