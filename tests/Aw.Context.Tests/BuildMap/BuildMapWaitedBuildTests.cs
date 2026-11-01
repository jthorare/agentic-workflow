using Aw.Context.Core.Extract;
using Aw.Context.Core.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.BuildMap;

/// <summary>A build that waited at <see cref="MapBuildGate"/> must not map again the heads the build ahead of it just saved (#1696).</summary>
public sealed class BuildMapWaitedBuildTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_that_waited_returns_the_draft_the_build_ahead_of_it_saved_for_the_same_heads()
    {
        var ct = TestContext.Current.CancellationToken;
        var scope = MapWorld.HumanWith(MapWorld.OnlyFixtureRepo);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _world.Clusters.Hold = release.Task;

        var first = _world.Map.BuildMap(progress: null, scope, MapWorld.None).AsTask();
        await WaitUntil(() => _world.Clusters.Active == 1, ct);
        var second = _world.Map.BuildMap(progress: null, scope, MapWorld.None).AsTask();
        await Task.Delay(TimeSpan.FromMilliseconds(100), TimeProvider.System, ct);   // the second build is parked at the gate: it can't be inside the cluster source while the first holds it
        Assert.False(second.IsCompleted);
        Assert.Equal(1, _world.Clusters.Active);

        release.SetResult();
        var firstReport = (await first.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, ct)).Value;
        var secondReport = (await second.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, ct)).Value;

        Assert.Equal(firstReport.Draft.Number, secondReport.Draft.Number);
        Assert.Equal(1, (await _world.Store.Versions(ct)).Value.Count(v => v.Status == MapVersionStatus.Draft));
        _ = Assert.Single(_world.Clusters.Asked);   // the commit was mapped once
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
