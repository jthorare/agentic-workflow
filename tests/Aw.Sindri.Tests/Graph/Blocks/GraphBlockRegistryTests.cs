using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Tests.Graph.Blocks;

[Lane(TestLane.Unit)]
public sealed class GraphBlockRegistryTests
{
    private static GraphBlockRegistry Standard() => GraphBlockRegistry.Standard(new BlockFakes.RecordingNotifyPort(), new BlockFakes.ScriptedModelPort("{}"));

    [Fact]
    public void The_standard_registry_lists_its_block_types()
    {
        var registry = Standard();

        Assert.Equal(["collect", "condition", "connection_event", "for_each", "manual", "notify", "prompt", "run_agent", "schedule"], registry.Types.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["connection_event", "manual", "schedule"], registry.Types.Values.Where(t => t.IsTrigger).Select(t => t.Type).Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public void Find_returns_the_runnable_block_of_a_type_and_null_for_an_unknown_one()
    {
        var registry = Standard();

        _ = Assert.IsType<ConditionBlock>(registry.Find("condition"));
        Assert.Null(registry.Find("poll"));
    }

    [Fact]
    public void Two_blocks_with_one_type_name_are_refused()
    {
        var notify = new BlockFakes.RecordingNotifyPort();

        var error = Assert.Throws<ArgumentException>(() => new GraphBlockRegistry([new NotifyBlock(notify), new NotifyBlock(notify)]));

        Assert.Contains("notify", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_blocks_example_is_a_config_its_own_schema_accepts()
    {
        foreach (var block in Standard().Types.Values)
        {
            var example = System.Text.Json.Nodes.JsonNode.Parse(block.Example)!.AsObject();

            Assert.Empty(block.Config.Check(example));
        }
    }
}
