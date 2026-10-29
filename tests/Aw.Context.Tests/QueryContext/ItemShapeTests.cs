using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Tests.Map;

namespace Aw.Context.Tests.QueryContext;

/// <summary>
/// The item shape (S-5B, spec §8.1, Phase 5): a tracker key or record key anchors the query on the record its sync linked, the nodes it touches
/// and the decisions it holds, read through the map with the intent layer merged in.
/// </summary>
[Trait("Lane", "integration")]
public sealed class ItemShapeTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static ContextQuery Item(string key) => new(Text: null, Item: key);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task A_blank_item_is_a_query_error(string item)
    {
        using var world = new MapWorld();
        _ = await world.ImportFixture();

        var result = await world.Engine.Query(Item(item), MapWorld.Human, Ct);

        Assert.Equal(ContextErrors.QueryInvalid.Code, result.Error.Code);
    }
}
