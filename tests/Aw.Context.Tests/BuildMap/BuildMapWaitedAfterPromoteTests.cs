using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.BuildMap;

/// <summary>#1775: a map refresh promotes its draft as soon as its build releases the gate; a build parked behind it that reads the versions after that finds no draft, only the version now current.</summary>
public sealed class BuildMapWaitedAfterPromoteTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_that_waited_behind_one_whose_draft_was_published_before_it_looked_returns_that_version()
    {
        var ct = TestContext.Current.CancellationToken;
        var scope = MapWorld.HumanWith(MapWorld.OnlyFixtureRepo);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _world.Store.SaveDraftHold = release.Task;
        _world.Store.PromoteOnSave = true;   // the first build's draft is current by the time the gate is free

        var first = _world.Map.BuildMap(progress: null, scope, MapWorld.None).AsTask();
        await WaitUntil(() => _world.Store.SaveDraftsWaiting == 1, ct);
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
