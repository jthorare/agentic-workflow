using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Tests.Graph.Blocks;

[Lane(TestLane.Unit)]
public sealed class ConditionBlockTests
{
    [Fact]
    public async Task A_false_condition_takes_only_the_false_edge()
    {
        var context = BlockFakes.Context(new JsonObject { ["when"] = "agent.drifted" }, new(StringComparer.Ordinal) { ["agent.drifted"] = false });

        var result = await new ConditionBlock().Execute(context, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(["false"], result.Value.Taken);
        Assert.False(result.Value.Outputs["result"]?.GetValue<bool>());
    }

    [Fact]
    public async Task A_true_condition_takes_only_the_true_edge()
    {
        var context = BlockFakes.Context(new JsonObject { ["when"] = "agent.drifted and count > 2" }, new(StringComparer.Ordinal) { ["agent.drifted"] = true, ["count"] = 3 });

        var result = await new ConditionBlock().Execute(context, TestContext.Current.CancellationToken);

        Assert.Equal(["true"], result.Value.Taken);
    }

    [Fact]
    public async Task A_condition_that_does_not_parse_fails_the_node_with_the_reason()
    {
        var context = BlockFakes.Context(new JsonObject { ["when"] = "agent.drifted ==" });

        var result = await new ConditionBlock().Execute(context, TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("condition", result.Error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_condition_block_declares_the_true_and_false_branches()
    {
        var block = new ConditionBlock();

        Assert.Equal("condition", block.Type);
        Assert.False(block.IsTrigger);
        Assert.Equal(["true", "false"], block.OutputsFor(new JsonObject { ["when"] = "a" }).BranchNames);
    }
}
