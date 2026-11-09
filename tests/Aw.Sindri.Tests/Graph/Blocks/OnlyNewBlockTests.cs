using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;

namespace Aw.Sindri.Tests.Graph.Blocks;

[Lane(TestLane.Unit)]
public sealed class OnlyNewBlockTests
{
    private static BlockContext Context(JsonArray items, string? key = "id") =>
        BlockFakes.Context(key is null ? new JsonObject { ["items"] = items } : new JsonObject { ["items"] = items, ["key"] = key }, new(StringComparer.Ordinal) { ["workflow.id"] = FlowFakes.WorkflowA });

    private static JsonArray Items(params int[] ids) => [.. ids.Select(static i => (JsonNode)new JsonObject { ["id"] = i, ["title"] = "t" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) })];

    private static async Task<BlockResult> Run(OnlyNewBlock block, BlockContext context)
    {
        var result = await block.Execute(context, TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        return result.Value;
    }

    [Fact]
    public async Task The_first_run_passes_every_item_on_and_after_its_node_succeeded_the_second_passes_none()
    {
        var store = new FlowFakes.MemorySeenStore();
        var block = new OnlyNewBlock(store);
        var context = Context(Items(1, 2, 3));

        var first = await Run(block, context);
        Assert.True((await block.Commit(context, first, TestContext.Current.CancellationToken)).IsSuccess);
        var second = await Run(block, context);

        Assert.Equal([1, 2, 3], first.Outputs["items"]!.AsArray().Select(static i => i!["id"]!.GetValue<int>()));
        Assert.Equal(3, first.Outputs["count"]!.GetValue<int>());
        Assert.Empty(second.Outputs["items"]!.AsArray());
        Assert.Equal(3, second.Outputs["skipped"]!.GetValue<int>());
    }

    [Fact]
    public async Task Execute_alone_does_not_mark_its_items_as_seen()
    {
        var store = new FlowFakes.MemorySeenStore();
        var block = new OnlyNewBlock(store);
        var context = Context(Items(1, 2));

        _ = await Run(block, context);   // Execute alone writes nothing; the run-level proof (a failed run later in the graph) is GraphCommitTests.A_run_that_fails_after_only_new_gives_its_items_back_to_the_next_run
        var again = await Run(block, context);

        Assert.Equal(0, store.Writes);
        Assert.Equal(2, again.Outputs["items"]!.AsArray().Count);
    }

    [Fact]
    public async Task A_commit_that_cannot_write_fails_the_node_so_the_items_come_back()
    {
        var store = new FlowFakes.MemorySeenStore { FailWrites = SindriErrors.LedgerUnavailable.With("disk full") };
        var block = new OnlyNewBlock(store);
        var context = Context(Items(1));

        var committed = await block.Commit(context, await Run(block, context), TestContext.Current.CancellationToken);

        Assert.True(committed.IsError);
        Assert.Contains("disk full", committed.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Only_the_items_not_seen_before_pass_and_each_workflow_remembers_alone()
    {
        var store = new FlowFakes.MemorySeenStore();
        var block = new OnlyNewBlock(store);
        var first = Context(Items(1, 2));
        _ = await block.Commit(first, await Run(block, first), TestContext.Current.CancellationToken);

        var next = await Run(block, Context(Items(2, 3, 3, 4)));
        var other = await Run(block, BlockFakes.Context(new JsonObject { ["items"] = Items(1), ["key"] = "id" }, new(StringComparer.Ordinal) { ["workflow.id"] = FlowFakes.WorkflowB }));

        Assert.Equal([3, 4], next.Outputs["items"]!.AsArray().Select(static i => i!["id"]!.GetValue<int>()));   // 2 was seen, the second 3 is a repeat in the same list
        _ = Assert.Single(other.Outputs["items"]!.AsArray());
    }

    [Fact]
    public async Task Without_a_key_the_item_itself_is_the_key()
    {
        var block = new OnlyNewBlock(new FlowFakes.MemorySeenStore());
        var context = Context(["a", "b", "a"], key: null);

        var result = await Run(block, context);

        Assert.Equal(["a", "b"], result.Outputs["items"]!.AsArray().Select(static i => i!.GetValue<string>()), StringComparer.Ordinal);
    }

    [Fact]
    public async Task An_item_without_the_key_field_or_a_list_that_is_not_a_list_fails_the_node()
    {
        var block = new OnlyNewBlock(new FlowFakes.MemorySeenStore());

        var noKey = await block.Execute(Context([new JsonObject { ["title"] = "x" }]), TestContext.Current.CancellationToken);
        var notList = await block.Execute(BlockFakes.Context(new JsonObject { ["items"] = "oops" }, new(StringComparer.Ordinal) { ["workflow.id"] = FlowFakes.WorkflowA }), TestContext.Current.CancellationToken);
        var noWorkflow = await block.Execute(BlockFakes.Context(new JsonObject { ["items"] = Items(1), ["key"] = "id" }), TestContext.Current.CancellationToken);

        Assert.Contains("'id'", noKey.Error.Message, StringComparison.Ordinal);
        Assert.Contains("list", notList.Error.Message, StringComparison.Ordinal);
        Assert.True(noWorkflow.IsError);
    }

    [Fact]
    public void The_block_takes_a_list_reference_and_an_optional_key_and_outputs_items_count_and_skipped()
    {
        var block = new OnlyNewBlock(new FlowFakes.MemorySeenStore());

        Assert.Equal("only_new", block.Type);
        Assert.False(block.IsTrigger);
        Assert.Empty(block.Config.Check(new JsonObject { ["items"] = "{{search.results}}" }));
        Assert.Equal(["count", "items", "skipped"], block.OutputsFor([]).Fields.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }
}
