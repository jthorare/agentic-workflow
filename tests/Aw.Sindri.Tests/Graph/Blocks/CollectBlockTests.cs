using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Tests.Graph.Blocks;

[Lane(TestLane.Unit)]
public sealed class CollectBlockTests
{
    [Fact]
    public async Task One_items_run_gives_its_value_unchanged()
    {
        var config = new JsonObject { ["value"] = new JsonObject { ["n"] = 2 } };

        var result = await new CollectBlock().Execute(BlockFakes.Context(config), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Outputs["value"]?["n"]?.GetValue<int>());
    }

    [Fact]
    public async Task A_missing_value_is_null_not_a_failure()
    {
        var config = new JsonObject { ["value"] = null };

        var result = await new CollectBlock().Execute(BlockFakes.Context(config), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Outputs["value"]);
    }

    [Fact]
    public void The_block_declares_the_gathered_items_and_count_and_its_example_fits_its_schema()
    {
        var block = new CollectBlock();

        Assert.Equal("collect", block.Type);
        Assert.Equal("array", block.OutputsFor([]).Fields["items"]);
        Assert.Equal("number", block.OutputsFor([]).Fields["count"]);
        Assert.Empty(block.Config.Check((JsonObject)JsonNode.Parse(block.Example)!));
    }
}
