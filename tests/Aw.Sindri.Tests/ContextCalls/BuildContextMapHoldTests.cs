using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>Issue 1041: a hand-built draft with nothing left to decide is held, so the context-map-refresh service's publish step promotes it; one with decisions left is not.</summary>
public sealed class BuildContextMapHoldTests
{
    private static readonly DateTimeOffset _now = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
    private readonly SliceHarness _h = new();
    private readonly RefreshEngineProxy _engine = RefreshEngineProxy.Create();
    private readonly FakeJobContextAccessor _jobs = new();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<Result<BuildContextMapResult>> Run(int undecided)
    {
        _h.Clock.SetUtcNow(_now);
        _engine.Queue = new ReviewQueueView("v2@abc123", ReviewVersion.None, [], 0, 0, new QueueCounts(undecided, 0, 0), new Dictionary<string, int>(StringComparer.Ordinal), ReviewTruncated.None);
        _ = await _h.HoldLease();
        _ = await _h.Profiles.Save(SliceHarness.Profile(), Ct);
        var service = new BuildContextMapService(new ContextScopes(_h.Profiles, _h.Callers), _engine.Proxy, new MapEventSource(_h.Held, _h.Store, _h.Events), new ScriptedOutlooks(), _jobs, _h.Clock);
        using var job = _jobs.Enter(new JobContext(JobId.New(_h.Ids), JobKind.From("BuildContextMap"), new Progress<string>()));
        using var caller = _h.Callers.EnterService("context-map-refresh");
        return await service.Handle(new BuildContextMapRequest(), Ct);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_draft_with_nothing_to_decide_is_held_for_the_publish_step()
    {
        var result = await Run(undecided: 0);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Code : null);
        var hold = Assert.Single(_engine.Holds);
        Assert.Equal(2, hold.Version);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_draft_with_decisions_left_waits_for_a_persons_promote()
    {
        var result = await Run(undecided: 3);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.Code : null);
        Assert.Empty(_engine.Holds);
    }
}
