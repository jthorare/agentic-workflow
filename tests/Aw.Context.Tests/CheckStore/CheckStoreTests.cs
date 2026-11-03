using Aw.Context.Core.Map;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.CheckStore;

public sealed class CheckStoreTests : IDisposable
{
    private readonly MapWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task The_facade_returns_the_context_store_row_for_the_stores_files()
    {
        _world.StoreFiles.Current = new StoreFileSizes(100L * 1024 * 1024, 2L * 1024 * 1024 * 1024);

        var check = (await _world.Map.CheckStore(MapWorld.None)).Value;

        Assert.Equal(("context:store", MapHealthState.Warn), (check.Name, check.State));
    }
}
