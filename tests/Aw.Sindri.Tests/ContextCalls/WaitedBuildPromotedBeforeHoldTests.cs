using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Review;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>
/// #1882, the second window: the publish gate decides to publish the reused draft, and the draft is promoted before the build stores its hold, so the store answers SND-CONTEXT-190 although the draft is now
/// the current map. The build still succeeds with the reused version. The engine double promotes inside the hold call, so no timing is involved.
/// </summary>
public sealed class WaitedBuildPromotedBeforeHoldTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    private const string Reused = "v1 was built from these heads while this build waited, so it was not built again";
    private readonly SliceHarness _h = new();
    private readonly FakeJobContextAccessor _jobs = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_waited_build_whose_draft_is_promoted_before_its_hold_is_stored_succeeds()
    {
        _h.Clock.SetUtcNow(_now);
        var inner = RefreshEngineProxy.Create();
        var heads = new[] { new MapHead(ContextRepoName.From("app"), "abc123", OnDefault: true) };
        inner.Built = new MapBuildReport(new MapVersion(1, ContextRepoName.From("app"), "abc123", KeyVersion: 1, DateTimeOffset.UnixEpoch, MapVersionStatus.Draft), [], [], heads) { Notes = [Reused] };
        inner.Queue = new ReviewQueueView("v1@abc123", ReviewVersion.None, [], 0, 0, new QueueCounts(0, 0, 0), new Dictionary<string, int>(StringComparer.Ordinal), ReviewTruncated.None);
        inner.State = new MapPublishState(Hold: null, LatestDraft: 1, Current: null, CurrentHeads: []);
        var outlooks = new ScriptedOutlooks();
        _ = await _h.HoldLease();
        _ = await _h.Profiles.Save(SliceHarness.Profile(), Ct);
        var service = new BuildContextMapService(new ContextScopes(_h.Profiles, _h.Callers, new FakeRepoCheckouts()), PromotingOnHoldEngine.Over(inner), new MapEventSource(_h.Held, _h.Store, _h.Events), outlooks, _jobs, _h.Clock);
        using var job = _jobs.Enter(new JobContext(JobId.New(_h.Ids), JobKind.From("BuildContextMap"), new Progress<string>()));
        using var caller = _h.Callers.EnterService("context-map-build");

        var result = await service.Handle(new BuildContextMapRequest(), Ct);

        Assert.True(result.IsSuccess, result.IsError ? $"{result.Error.Code}: {result.Error.Message}" : null);
        Assert.Equal(1, result.Value.Draft);
        Assert.Equal([Reused], result.Value.Notes);
        _ = Assert.Single(inner.Holds);
        // The gate published: with every outlook absent MapPublishDecision can only publish or drop, never wait, and it read the outlooks once, so the one hold is the build's final one, not the gate's wait hold.
        Assert.Equal(IndexOutlook.Absent, outlooks.All);
        Assert.Equal(1, outlooks.Reads);
    }
}
