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

    [Fact]
    public async Task Every_block_one_of_names_real_fields_of_its_own_schema()
    {
        var listed = await new ListWorkflowBlocksService(BlockFieldCaptionsTests.Everything(), new Docs()).Handle(new ListWorkflowBlocksRequest(), Token);
        Assert.True(listed.IsSuccess);

        var unknown = listed.Value.Groups.SelectMany(static g => g.Blocks)
            .SelectMany(static b => (b.OneOf ?? []).SelectMany(static g => g).Where(n => !b.Config.Any(f => string.Equals(f.Name, n, StringComparison.Ordinal))).Select(n => $"{b.Type}.{n}"))
            .ToList();

        Assert.True(unknown.Count == 0, "oneOf names that are not fields (the editor would drop the rule): " + string.Join(", ", unknown));   // OneOfProblem skips a group with fewer than two matching fields
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
