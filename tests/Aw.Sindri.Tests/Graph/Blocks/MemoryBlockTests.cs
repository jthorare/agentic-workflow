using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Tests.Graph.Blocks;

/// <summary>The memory blocks (agent-first PR 13 follow-up): 'memory_search' (Recall), 'memory_write' (Remember) and 'memory_forget' (Forget) reach the Sindri memory stores through <see cref="IGraphMemoryPort"/>.</summary>
[Lane(TestLane.Unit)]
public sealed class MemoryBlockTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Recall_passes_the_memory_query_level_and_limit_and_hands_on_the_fact_texts_and_the_mode()
    {
        var port = new BlockFakes.RecordingMemoryPort { Recalled = new MemoryRecalled(["Checked api at abc", "PR 12 is open"], "semantic") };

        var result = await new MemorySearchBlock(port).Execute(BlockFakes.Context(new JsonObject { ["memory"] = "drift-check", ["query"] = "last commit", ["level"] = "strict", ["limit"] = 3 }), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        Assert.Equal(new MemoryToRecall("drift-check", "last commit", "strict", 3), Assert.Single(port.Recalls));
        Assert.Equal(["Checked api at abc", "PR 12 is open"], result.Value.Outputs["facts"]!.AsArray().Select(static f => f!.GetValue<string>()), StringComparer.Ordinal);
        Assert.Equal("semantic", result.Value.Outputs["mode"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(null, "q")]
    [InlineData("drift-check", null)]
    [InlineData("drift-check", "   ")]
    public async Task Recall_without_a_memory_or_a_query_fails_the_node_with_the_reason(string? memory, string? query)
    {
        var config = new JsonObject { ["memory"] = memory, ["query"] = query };
        var port = new BlockFakes.RecordingMemoryPort();

        var result = await new MemorySearchBlock(port).Execute(BlockFakes.Context(config), Token);

        Assert.Equal(SindriErrors.GraphBlockFailed.Code, result.Error.Code);
        Assert.Empty(port.Recalls);
    }

    [Fact]
    public async Task A_recall_the_port_refuses_fails_the_node_with_the_port_error()
    {
        var port = new BlockFakes.RecordingMemoryPort { Fail = SindriErrors.InvalidInput.With("no such memory") };

        var result = await new MemorySearchBlock(port).Execute(BlockFakes.Context(new JsonObject { ["memory"] = "nope", ["query"] = "q" }), Token);

        Assert.Equal(SindriErrors.InvalidInput.Code, result.Error.Code);
    }

    [Fact]
    public async Task Remember_passes_the_run_memory_fact_tags_and_expiry_and_hands_on_the_id_and_action()
    {
        var port = new BlockFakes.RecordingMemoryPort { Remembered = new MemoryRemembered("mf_1", "refreshed") };
        var config = new JsonObject { ["memory"] = "drift-check", ["fact"] = "  Checked api at abc  ", ["tags"] = new JsonArray("checks", "api"), ["expires"] = "30d" };

        var result = await new MemoryWriteBlock(port).Execute(BlockFakes.Context(config), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var write = Assert.Single(port.Writes);
        Assert.Equal(("wf@1:1", "drift-check", "Checked api at abc", TimeSpan.FromDays(30)), (write.Run.Value, write.Memory, write.Fact, write.ExpiresIn));
        Assert.Equal(["checks", "api"], write.Tags);
        Assert.Equal(("mf_1", "refreshed"), (result.Value.Outputs["id"]!.GetValue<string>(), result.Value.Outputs["action"]!.GetValue<string>()));
    }

    [Fact]
    public async Task Remember_with_no_tags_and_no_expiry_writes_a_fact_that_never_expires()
    {
        var port = new BlockFakes.RecordingMemoryPort();

        var result = await new MemoryWriteBlock(port).Execute(BlockFakes.Context(new JsonObject { ["memory"] = "m", ["fact"] = "f" }), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var write = Assert.Single(port.Writes);
        Assert.Empty(write.Tags);
        Assert.Null(write.ExpiresIn);
    }

    [Theory]
    [InlineData("m", "   ", null)]
    [InlineData("m", "f", "soon")]
    [InlineData(null, "f", null)]
    public async Task Remember_refuses_a_missing_memory_a_blank_fact_and_an_expiry_that_is_not_a_duration(string? memory, string? fact, string? expires)
    {
        var port = new BlockFakes.RecordingMemoryPort();

        var result = await new MemoryWriteBlock(port).Execute(BlockFakes.Context(new JsonObject { ["memory"] = memory, ["fact"] = fact, ["expires"] = expires }), Token);

        Assert.Equal(SindriErrors.GraphBlockFailed.Code, result.Error.Code);
        Assert.Empty(port.Writes);
    }

    [Fact]
    public async Task Forget_by_fact_id_and_forget_by_tag_hand_on_the_count()
    {
        var port = new BlockFakes.RecordingMemoryPort { Forgotten = 2 };
        var block = new MemoryForgetBlock(port);

        var byFact = await block.Execute(BlockFakes.Context(new JsonObject { ["memory"] = "m", ["fact"] = "mf_1" }), Token);
        var byTag = await block.Execute(BlockFakes.Context(new JsonObject { ["memory"] = "m", ["tag"] = "stale" }), Token);

        Assert.Equal([new MemoryToForget("m", Fact: "mf_1", Tag: null), new MemoryToForget("m", Fact: null, Tag: "stale")], port.Forgets);
        Assert.Equal((2, 2), (byFact.Value.Outputs["count"]!.GetValue<int>(), byTag.Value.Outputs["count"]!.GetValue<int>()));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("mf_1", "stale")]
    public async Task Forget_needs_exactly_one_of_fact_and_tag(string? fact, string? tag)
    {
        var port = new BlockFakes.RecordingMemoryPort();

        var result = await new MemoryForgetBlock(port).Execute(BlockFakes.Context(new JsonObject { ["memory"] = "m", ["fact"] = fact, ["tag"] = tag }), Token);

        Assert.Equal(SindriErrors.GraphBlockFailed.Code, result.Error.Code);
        Assert.Empty(port.Forgets);
    }

    [Fact]
    public void The_three_blocks_carry_their_picker_labels_groups_and_output_fields()
    {
        var port = new BlockFakes.RecordingMemoryPort();
        var search = new MemorySearchBlock(port);
        var write = new MemoryWriteBlock(port);
        var forget = new MemoryForgetBlock(port);

        Assert.Equal([("memory_search", "Recall", BlockGroup.Data), ("memory_write", "Remember", BlockGroup.Logic), ("memory_forget", "Forget", BlockGroup.Logic)],
            new IGraphBlock[] { search, write, forget }.Select(static b => (b.Type, b.Title, b.Group)));
        Assert.Equal(["facts", "mode"], search.OutputsFor([]).Fields.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["action", "id"], write.OutputsFor([]).Fields.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
        Assert.Equal(["count"], forget.OutputsFor([]).Fields.Keys, StringComparer.Ordinal);
    }

    [Fact]
    public void The_registry_adds_the_three_memory_blocks_and_each_examples_config_fits_its_schema()
    {
        var blocks = GraphBlockRegistry.MemoryBlocks(new BlockFakes.RecordingMemoryPort());

        Assert.Equal(["memory_forget", "memory_search", "memory_write"], blocks.Select(static b => b.Type).Order(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var block in blocks)
        {
            var example = (JsonObject)JsonNode.Parse(block.Example)!;
            Assert.All(example.Select(static p => p.Key), key => Assert.Contains(key, block.Config.Fields.Select(static f => f.Name), StringComparer.Ordinal));
            Assert.All(block.Config.Fields.Where(static f => f.Required), field => Assert.True(example.ContainsKey(field.Name), $"{block.Type} example lacks '{field.Name}'"));
        }
    }
}
