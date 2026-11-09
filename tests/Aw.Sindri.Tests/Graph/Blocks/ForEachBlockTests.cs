using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Tests.Graph.Blocks;

[Lane(TestLane.Unit)]
public sealed class ForEachBlockTests
{
    private static ForEachBlock Block(int maxItems = ForEachLimits.DefaultMaxItems, int batchSize = ForEachLimits.DefaultBatchSize) => new(new Fixed(new ForEachLimits(maxItems, batchSize)));

    private static JsonArray Items(int count) => [.. Enumerable.Range(1, count).Select(static i => (JsonNode?)JsonValue.Create(i))];

    [Fact]
    public async Task A_list_inside_the_cap_gives_its_count_and_the_default_batch_size()
    {
        var context = BlockFakes.Context(new JsonObject { ["items"] = Items(3) });

        var result = await Block().Execute(context, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Outputs["count"]?.GetValue<int>());
        Assert.Equal(20, result.Value.Outputs["batchSize"]?.GetValue<int>());
        Assert.Equal(["out"], result.Value.Taken);
    }

    [Fact]
    public async Task An_empty_list_is_a_loop_of_zero_items()
    {
        var result = await Block().Execute(BlockFakes.Context(new JsonObject { ["items"] = new JsonArray() }), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Outputs["count"]?.GetValue<int>());
    }

    [Fact]
    public async Task More_items_than_the_profile_cap_fail_the_node_and_name_both_numbers()
    {
        var result = await Block(maxItems: 5).Execute(BlockFakes.Context(new JsonObject { ["items"] = Items(6) }), TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("6", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("maxItems 5", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_node_maxItems_below_the_profile_cap_lowers_the_cap()
    {
        var config = new JsonObject { ["items"] = Items(6), ["maxItems"] = 3, ["batchSize"] = 4 };

        var result = await Block(maxItems: 5).Execute(BlockFakes.Context(config), TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("maxItems 3", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_node_maxItems_above_the_profile_cap_is_clamped_to_the_profile_cap()
    {
        var config = new JsonObject { ["items"] = Items(6), ["maxItems"] = 10, ["batchSize"] = 4 };

        var result = await Block(maxItems: 5).Execute(BlockFakes.Context(config), TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("maxItems 5", result.Error.Message, StringComparison.Ordinal);
        Assert.Contains("profile", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_node_maxItems_inside_the_profile_cap_passes()
    {
        var config = new JsonObject { ["items"] = Items(4), ["maxItems"] = 4, ["batchSize"] = 4 };

        var result = await Block(maxItems: 5).Execute(BlockFakes.Context(config), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, result.Value.Outputs["batchSize"]?.GetValue<int>());
    }

    [Theory]
    [InlineData("maxItems", 0)]
    [InlineData("maxItems", 10_001)]
    [InlineData("batchSize", 0)]
    [InlineData("batchSize", 101)]
    public async Task A_cap_or_batch_size_outside_its_range_fails_the_node(string field, int value)
    {
        var config = new JsonObject { ["items"] = Items(1), [field] = value };

        var result = await Block().Execute(BlockFakes.Context(config), TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains(field, result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Items_that_are_not_a_list_fail_the_node()
    {
        var result = await Block().Execute(BlockFakes.Context(new JsonObject { ["items"] = "a, b" }), TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("list", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_block_declares_count_and_batch_size_and_its_example_fits_its_schema()
    {
        var block = Block();

        Assert.Equal("for_each", block.Type);
        Assert.False(block.IsTrigger);
        Assert.Equal(["batchSize", "count"], block.OutputsFor([]).Fields.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Empty(block.Config.Check((JsonObject)JsonNode.Parse(block.Example)!));
    }

    private sealed class Fixed(ForEachLimits limits) : IForEachLimitSource
    {
        public ValueTask<ForEachLimits> Current(CancellationToken cancellationToken) => ValueTask.FromResult(limits);
    }
}
