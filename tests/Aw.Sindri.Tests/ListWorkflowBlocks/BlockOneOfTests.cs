using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Docs;
using Aw.Sindri.Features.ListWorkflowBlocks;

namespace Aw.Sindri.Tests.ListWorkflowBlocks;

/// <summary>GET /sindri/workflows/blocks: a block with an "exactly one of these fields" rule serves it as "oneOf", so the editor keeps Save off until the rule holds (#4359).</summary>
[Lane(TestLane.Unit)]
public sealed class BlockOneOfTests
{
    private static readonly JsonSerializerOptions _web = new(JsonSerializerDefaults.Web);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Wait_serves_duration_and_until_as_one_group()
    {
        var wait = await View("wait");

        Assert.Equal([["duration", "until"]], wait.OneOf!.Select(static g => g.ToArray()));
    }

    [Fact]
    public async Task A_block_with_no_such_rule_leaves_oneOf_out_of_the_json()
    {
        var schedule = await View("schedule");

        var json = JsonSerializer.SerializeToNode(schedule, _web)!.AsObject();

        Assert.Null(schedule.OneOf);
        Assert.False(json.ContainsKey("oneOf"));
    }

    private static async Task<BlockView> View(string type)
    {
        var service = new ListWorkflowBlocksService(new Catalog(new ScheduleBlock(), new WaitBlock(TimeProvider.System)), new Docs());
        var listed = await service.Handle(new ListWorkflowBlocksRequest(), Token);
        Assert.True(listed.IsSuccess);
        return listed.Value.Groups.SelectMany(static g => g.Blocks).Single(b => string.Equals(b.Type, type, StringComparison.Ordinal));
    }

    private sealed class Catalog(params IGraphBlockType[] blocks) : IGraphBlockCatalog
    {
        public IReadOnlyDictionary<string, IGraphBlockType> Types { get; } = blocks.ToDictionary(static b => b.Type, StringComparer.Ordinal);
    }

    private sealed class Docs : IWorkflowDocs
    {
        public IReadOnlyList<WorkflowDocPage> Pages => [];
    }
}
