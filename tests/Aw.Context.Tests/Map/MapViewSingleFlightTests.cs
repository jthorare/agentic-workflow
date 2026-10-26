using Aw.Context.Core.Linking;
using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Context.Tests.Map;

/// <summary>Callers that find no view for the same inputs together build it once; a build that throws is not kept.</summary>
public sealed class MapViewSingleFlightTests : IDisposable
{
    private const int Callers = 8;

    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task Concurrent_reads_on_a_cold_cache_build_the_view_once()
    {
        _ = await _world.ImportFixture();
        var map = (await new MapReader(_world.Store, _world.Intents).Read(MapWorld.Human, draft: false, MapWorld.None)).Value;
        var builds = 0;
        var cache = new MapViewCache { Built = () => Interlocked.Increment(ref builds) };
        using var release = new ManualResetEventSlim();
        Result<MapView> Build()
        {
            release.Wait(MapWorld.None);
            return MapAccess.View(map.Graph, map.Snapshot.Overrides, MapWorld.Human, pending: false);
        }

        var calls = Enumerable.Range(0, Callers).Select(_ => Task.Run(() => cache.View(map.Snapshot, IntentLayer.Empty, MapWorld.Human, "mode", Build), MapWorld.None)).ToList();
        await Task.Delay(100, MapWorld.None);
        release.Set();
        var views = await Task.WhenAll(calls);

        Assert.Equal(1, builds);
        Assert.All(views, v => Assert.Same(views[0], v));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_build_that_throws_is_not_kept()
    {
        _ = await _world.ImportFixture();
        var map = (await new MapReader(_world.Store, _world.Intents).Read(MapWorld.Human, draft: false, MapWorld.None)).Value;
        var cache = new MapViewCache();
        var attempts = 0;
        Result<MapView> Build()
        {
            attempts++;
            return attempts == 1 ? throw new InvalidOperationException("boom") : MapAccess.View(map.Graph, map.Snapshot.Overrides, MapWorld.Human, pending: false);
        }

        _ = Assert.Throws<InvalidOperationException>(() => cache.View(map.Snapshot, IntentLayer.Empty, MapWorld.Human, "mode", Build));
        var again = cache.View(map.Snapshot, IntentLayer.Empty, MapWorld.Human, "mode", Build);

        Assert.True(again.IsSuccess);
        Assert.Equal(2, attempts);
    }
}
