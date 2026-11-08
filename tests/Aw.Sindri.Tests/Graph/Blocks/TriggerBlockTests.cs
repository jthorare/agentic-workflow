using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Tests.Graph.Blocks;

/// <summary>The two blocks that start a run.</summary>
[Lane(TestLane.Unit)]
public sealed class TriggerBlockTests
{
    [Fact]
    public void The_schedule_block_is_a_trigger_that_takes_a_cron_and_hands_on_the_tick()
    {
        var block = new ScheduleBlock();

        Assert.Equal("schedule", block.Type);
        Assert.True(block.IsTrigger);
        Assert.Empty(block.Config.Check(new JsonObject { ["cron"] = "0 7 * * 1-5", ["timezone"] = "America/Los_Angeles" }));
        Assert.Contains("scheduledFor", block.OutputsFor(JsonNode.Parse("{}")!.AsObject()).Fields.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public async Task The_schedule_block_outputs_the_tick_it_fired_for()
    {
        var context = BlockFakes.Context(new JsonObject { ["cron"] = "0 7 * * 1-5" }, new(StringComparer.Ordinal) { ["trigger.scheduledFor"] = "2026-11-09T07:00:00Z" });

        var result = await new ScheduleBlock().Execute(context, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("2026-11-09T07:00:00Z", result.Value.Outputs["scheduledFor"]?.GetValue<string>());
        Assert.Equal(["out"], result.Value.Taken);
    }

    [Fact]
    public async Task The_manual_block_is_a_trigger_with_no_config_that_hands_on_the_payload_it_was_started_with()
    {
        var block = new ManualBlock();
        var payload = new JsonObject { ["pr"] = 12 };

        var result = await block.Execute(BlockFakes.Context(JsonNode.Parse("{}")!.AsObject(), new(StringComparer.Ordinal) { ["trigger"] = payload }), TestContext.Current.CancellationToken);

        Assert.Equal("manual", block.Type);
        Assert.True(block.IsTrigger);
        Assert.Empty(block.Config.Fields);
        Assert.Equal(12, result.Value.Outputs["payload"]?["pr"]?.GetValue<int>());
    }
}
