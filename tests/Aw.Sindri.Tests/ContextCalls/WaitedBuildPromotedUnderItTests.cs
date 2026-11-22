using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Review;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>
/// #1882: a build that waited behind another gets that build's draft back (Status=Draft). If the draft is promoted after the waited build read its review queue and before its publish gate read the
/// stored state, the draft is the current map and no draft is left; the build still succeeds with the reused version instead of failing with SND-CONTEXT-190. The order is forced by the engine
/// double, which promotes inside the review-queue call, so no timing is involved.
/// </summary>
public sealed class WaitedBuildPromotedUnderItTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    private const string Reused = "v1 was built from these heads while this build waited, so it was not built again";
    private readonly SliceHarness _h = new();
    private readonly FakeJobContextAccessor _jobs = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_waited_build_whose_reused_draft_is_promoted_before_its_gate_succeeds_with_the_reused_version()
    {
        _h.Clock.SetUtcNow(_now);
        var inner = RefreshEngineProxy.Create();
        var heads = new[] { new MapHead(ContextRepoName.From("app"), "abc123", OnDefault: true) };
        inner.Built = new MapBuildReport(new MapVersion(1, ContextRepoName.From("app"), "abc123", KeyVersion: 1, DateTimeOffset.UnixEpoch, MapVersionStatus.Draft), [], [], heads) { Notes = [Reused] };
        inner.Queue = new ReviewQueueView("v1@abc123", ReviewVersion.None, [], 0, 0, new QueueCounts(0, 0, 0), new Dictionary<string, int>(StringComparer.Ordinal), ReviewTruncated.None);
        inner.State = new MapPublishState(Hold: null, LatestDraft: 1, Current: null, CurrentHeads: []);
        var engine = PromotingEngine.Over(inner);
        _ = await _h.HoldLease();
        _ = await _h.Profiles.Save(SliceHarness.Profile(), Ct);
        var service = new BuildContextMapService(new ContextScopes(_h.Profiles, _h.Callers, new FakeRepoCheckouts()), engine, new MapEventSource(_h.Held, _h.Store, _h.Events), new ScriptedOutlooks(), _jobs, _h.Clock);
        using var job = _jobs.Enter(new JobContext(JobId.New(_h.Ids), JobKind.From("BuildContextMap"), new Progress<string>()));
        using var caller = _h.Callers.EnterService("context-map-build");

        var result = await service.Handle(new BuildContextMapRequest(), Ct);

        Assert.True(result.IsSuccess, result.IsError ? $"{result.Error.Code}: {result.Error.Message}" : null);
        Assert.Equal(1, result.Value.Draft);
        Assert.Equal([Reused], result.Value.Notes);
    }
}
