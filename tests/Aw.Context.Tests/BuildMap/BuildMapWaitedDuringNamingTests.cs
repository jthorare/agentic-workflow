using Aw.Context.Core.Extract;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.BuildMap;

/// <summary>A draft is dated before its concepts are named (#1696): a build that asks for the gate meanwhile still reuses it, and one that waited behind a build which saved nothing still builds.</summary>
public sealed class BuildMapWaitedDuringNamingTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_that_asks_while_the_build_ahead_names_its_concepts_returns_that_build_draft()
    {
        var ct = TestContext.Current.CancellationToken;
        var scope = MapWorld.HumanWith(MapWorld.OnlyFixtureRepo);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _world.Store.SaveDraftHold = release.Task;

        var first = _world.Map.BuildMap(progress: null, scope, MapWorld.None).AsTask();
        await WaitUntil(() => _world.Store.SaveDraftsWaiting == 1, ct);   // the first build has stamped its time and is not yet saved
        _world.Clock.Advance(TimeSpan.FromMinutes(5));
        var second = _world.Map.BuildMap(progress: null, scope, MapWorld.None).AsTask();
        await Task.Delay(TimeSpan.FromMilliseconds(100), TimeProvider.System, ct);   // parked at the gate behind the first
        Assert.False(second.IsCompleted);

        release.SetResult();
        var firstReport = (await first.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, ct)).Value;
        var secondReport = (await second.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, ct)).Value;

        Assert.Equal(firstReport.Draft.Number, secondReport.Draft.Number);
        _ = Assert.Single(_world.Clusters.Asked);
        var number = firstReport.Draft.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal($"v{number} was built from these heads while this build waited, so it was not built again", Assert.Single(secondReport.Notes));
        Assert.DoesNotContain(firstReport.Notes, n => n.Contains("while this build waited", StringComparison.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_that_waited_behind_a_holder_that_saved_no_draft_builds_again()
    {
        var ct = TestContext.Current.CancellationToken;
        var scope = MapWorld.HumanWith(MapWorld.OnlyFixtureRepo);
        var earlier = (await _world.Map.BuildMap(progress: null, scope, MapWorld.None)).Value;
        var gate = _world.Service<MapBuildGate>();
        var holder = await gate.Enter(job: null, progress: null, ct);

        var waiting = _world.Map.BuildMap(progress: null, scope, MapWorld.None).AsTask();
        await Task.Delay(TimeSpan.FromMilliseconds(100), TimeProvider.System, ct);
        Assert.False(waiting.IsCompleted);
        holder.Dispose();
        var report = (await waiting.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, ct)).Value;

        Assert.NotEqual(earlier.Draft.Number, report.Draft.Number);
        Assert.Equal(2, _world.Clusters.Asked.Count);
        Assert.DoesNotContain(report.Notes, n => n.Contains("while this build waited", StringComparison.Ordinal));
    }

    private static async Task WaitUntil(Func<bool> condition, CancellationToken ct)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(TimeSpan.FromSeconds(10));
        while (!condition())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(5), TimeProvider.System, limit.Token);
        }
    }
}
