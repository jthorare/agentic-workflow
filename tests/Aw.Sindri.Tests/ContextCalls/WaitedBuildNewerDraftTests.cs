using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Review;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>
/// #1882, the other side: if the draft a waited build reused is replaced by a newer draft (and is not the current map) after the build read its review queue, the publish gate's SND-CONTEXT-190 stands:
/// there is nothing to treat as promoted. The engine double changes the stored state inside the review-queue call, so no timing is involved.
/// </summary>
public sealed class WaitedBuildNewerDraftTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    private const string Reused = "v1 was built from these heads while this build waited, so it was not built again";
    private readonly SliceHarness _h = new();
    private readonly FakeJobContextAccessor _jobs = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact, Trait("Lane", "unit")]
    public async Task A_waited_build_whose_draft_was_replaced_by_a_newer_one_still_fails_with_draft_changed()
    {
        _h.Clock.SetUtcNow(_now);
        var inner = RefreshEngineProxy.Create();
        var heads = new[] { new MapHead(ContextRepoName.From("app"), "abc123", OnDefault: true) };
        inner.Built = new MapBuildReport(new MapVersion(1, ContextRepoName.From("app"), "abc123", KeyVersion: 1, DateTimeOffset.UnixEpoch, MapVersionStatus.Draft), [], [], heads) { Notes = [Reused] };
        inner.Queue = new ReviewQueueView("v1@abc123", ReviewVersion.None, [], 0, 0, new QueueCounts(0, 0, 0), new Dictionary<string, int>(StringComparer.Ordinal), ReviewTruncated.None);
        inner.State = new MapPublishState(Hold: null, LatestDraft: 1, Current: null, CurrentHeads: []);
        var engine = ReplacingEngine.Over(inner);
        _ = await _h.HoldLease();
        _ = await _h.Profiles.Save(SliceHarness.Profile(), Ct);
        var service = new BuildContextMapService(new ContextScopes(_h.Profiles, _h.Callers, new FakeRepoCheckouts()), engine, new MapEventSource(_h.Held, _h.Store, _h.Events), new ScriptedOutlooks(), _jobs, _h.Clock);
        using var job = _jobs.Enter(new JobContext(JobId.New(_h.Ids), JobKind.From("BuildContextMap"), new Progress<string>()));
        using var caller = _h.Callers.EnterService("context-map-build");

        var result = await service.Handle(new BuildContextMapRequest(), Ct);

        Assert.True(result.IsError);
        Assert.Equal(ContextErrors.DraftChanged.Code, result.Error.Code);
    }
}
