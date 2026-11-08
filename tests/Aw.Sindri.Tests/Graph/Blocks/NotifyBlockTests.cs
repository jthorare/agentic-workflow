using System.Text.Json.Nodes;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Tests.Graph.Blocks;

[Lane(TestLane.Unit)]
public sealed class NotifyBlockTests
{
    [Fact]
    public async Task The_notify_block_writes_a_needs_you_row_with_the_interpolated_title_and_body()
    {
        var port = new BlockFakes.RecordingNotifyPort();
        var context = BlockFakes.Context(new JsonObject { ["title"] = "Drifted", ["body"] = "3 files drifted" }, node: "tell");

        var result = await new NotifyBlock(port).Execute(context, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal([("wf@1:1", "tell", "Drifted", (string?)"3 files drifted")], port.Posted);
        Assert.Equal(["out"], result.Value.Taken);
    }

    [Fact]
    public async Task A_notify_the_port_cannot_write_fails_the_node_with_the_ports_reason()
    {
        var port = new BlockFakes.RecordingNotifyPort { Fail = KernelErrors.Invalid.With("the ledger is read-only") };

        var result = await new NotifyBlock(port).Execute(BlockFakes.Context(new JsonObject { ["title"] = "x" }), TestContext.Current.CancellationToken);

        Assert.True(result.IsError);
        Assert.Contains("the ledger is read-only", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_notify_block_takes_a_title_and_an_optional_body()
    {
        var block = new NotifyBlock(new BlockFakes.RecordingNotifyPort());

        Assert.Equal("notify", block.Type);
        Assert.False(block.IsTrigger);
        Assert.Empty(block.Config.Check(new JsonObject { ["title"] = "t" }));
        Assert.Contains(block.Config.Check(new JsonObject { ["body"] = "b" }), p => p.Field == "title");
    }
}
