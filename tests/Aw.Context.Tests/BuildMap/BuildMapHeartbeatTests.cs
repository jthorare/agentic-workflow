using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.BuildMap;

/// <summary>#2106: a graphify run that takes 45 min under the default 60-min limit writes "running" lines during it, so the job's stall watch never ends a healthy build.</summary>
public sealed class BuildMapHeartbeatTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task A_cluster_source_that_takes_45_minutes_under_a_60_minute_limit_reports_it_is_running()
    {
        var ct = TestContext.Current.CancellationToken;
        var progress = new SyncProgress();
        _world.Clusters.Clock = _world.Clock;
        _world.Clusters.Takes = TimeSpan.FromMinutes(45);

        var build = _world.Map.BuildMap(progress, MapWorld.HumanWith(MapWorld.OnlyFixtureRepo), MapWorld.None).AsTask();
        await WaitUntil(() => _world.Clusters.Active == 1, ct);
        for (var i = 0; i < 50 && !build.IsCompleted; i++)
        {
            _world.Clock.Advance(TimeSpan.FromMinutes(1));
            await Task.Delay(TimeSpan.FromMilliseconds(10), TimeProvider.System, ct);
        }

        Assert.True(build.IsCompleted, "the build is still going after 50 min");
        _ = (await build.WaitAsync(TimeSpan.FromSeconds(10), TimeProvider.System, ct)).Value;
        Assert.Contains(progress.Lines, l => string.Equals(l, "graphify running, 5 min", StringComparison.Ordinal));
        Assert.Contains(progress.Lines, l => string.Equals(l, "graphify running, 40 min", StringComparison.Ordinal));
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
