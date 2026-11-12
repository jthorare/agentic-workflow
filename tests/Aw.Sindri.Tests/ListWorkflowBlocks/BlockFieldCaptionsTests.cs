using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Docs;
using Aw.Sindri.Features.ListWorkflowBlocks;
using Aw.Sindri.Tests.Graph.Blocks;
using Aw.Sindri.Tests.Toolkits;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.ListWorkflowBlocks;

/// <summary>GET /sindri/workflows/blocks: each config field carries a plain-words caption, optional closed choices, optional open presets and an advanced flag (#2807).</summary>
[Lane(TestLane.Unit)]
public sealed class BlockFieldCaptionsTests
{
    private static readonly JsonSerializerOptions _web = new(JsonSerializerDefaults.Web);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Schedule_cron_field_offers_weekday_presets()
    {
        var cron = await Field("schedule", "cron");

        Assert.Equal("Runs at", cron.Caption);
        Assert.False(cron.Advanced);
        Assert.Equal(
            [("0 7 * * 1-5", "Every weekday at 7:00"), ("0 9 * * *", "Every day at 9:00"), ("0 * * * *", "Every hour"), ("0 8 * * 1", "Mondays at 8:00")],
            cron.Presets!.Select(static p => (p.Value, p.Label)));
        Assert.Null(cron.Choices);
    }

    [Fact]
    public async Task Schedule_timezone_field_offers_zone_ids_as_presets_not_a_closed_list()
    {
        var timezone = await Field("schedule", "timezone");

        Assert.Equal("Time zone", timezone.Caption);
        Assert.Contains(timezone.Presets!, static c => c.Value == "America/Los_Angeles" && c.Label == "America/Los_Angeles");
        Assert.Null(timezone.Choices);   // the engine takes any IANA zone (WaitSchedule.Zone), so the list only fills, it never limits
    }

    [Fact]
    public async Task A_field_with_no_lists_leaves_choices_and_presets_out_of_the_json()
    {
        var plain = await Field("wait", "duration");

        var json = JsonSerializer.SerializeToNode(plain, _web)!.AsObject();

        Assert.False(json.ContainsKey("choices"));
        Assert.False(json.ContainsKey("presets"));
        Assert.Equal("Wait for", json["caption"]!.GetValue<string>());
    }

    // why: a caption equal to the key (`prompt`, `waitSeconds`) is the fallback in ListWorkflowBlocksService, so it means nobody wrote plain words (#2925)
    private static readonly string[] _keysThatAreAlreadyPlainWords = [];

    [Fact]
    public async Task Every_block_field_has_a_plain_words_caption()
    {
        var registry = GraphBlockRegistry.Standard(
            new BlockFakes.RecordingNotifyPort(),
            new BlockFakes.ScriptedModelPort("{}"),
            more: [.. GraphBlockRegistry.FlowBlocks(new FlowFakes.MemorySeenStore(), new FlowFakes.RecordingItemPort(), new FlowFakes.ScriptedCallPort(), new FlowFakes.ScriptedHttpPort()),
                .. GraphBlockRegistry.ParkingBlocks(new BlockFakes.RecordingQuestionPort(), new FakeTimeProvider()),
                .. GraphBlockRegistry.ResultBlocks(new FlowFakes.ScriptedResultPort()),
                .. GraphBlockRegistry.MemoryBlocks(new BlockFakes.RecordingMemoryPort()),
                new CallToolBlock(new FakeRunningToolkits([]), new NoConnections()),
                .. GraphBlockRegistry.CodeBlocks(new FlowFakes.ScriptedCodePort())]);
        var listed = await new ListWorkflowBlocksService(registry, new Docs()).Handle(new ListWorkflowBlocksRequest(), Token);
        Assert.True(listed.IsSuccess);

        var bare = listed.Value.Groups.SelectMany(static g => g.Blocks)
            .SelectMany(static b => b.Config.Select(f => (Block: b.Type, Field: f)))
            .Where(x => string.Equals(x.Field.Caption, x.Field.Name, StringComparison.Ordinal) && !_keysThatAreAlreadyPlainWords.Contains(x.Field.Name))
            .Select(static x => $"{x.Block}.{x.Field.Name}")
            .ToList();

        Assert.True(bare.Count == 0, "fields still captioned with their key: " + string.Join(", ", bare));
    }

    private sealed class NoConnections : IConnectionToolPort
    {
        public ValueTask<Result<IReadOnlyList<ConnectionTool>>> ListTools(string connection, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Failure<IReadOnlyList<ConnectionTool>>(SindriErrors.ConnectionNotFound.With($"no connection '{connection}'")));

        public ValueTask<Result<string>> Call(string connection, string tool, string argumentsJson, CancellationToken cancellationToken) =>
            ValueTask.FromResult(Results.Failure<string>(SindriErrors.ConnectionNotFound.With($"no connection '{connection}'")));
    }

    private static async Task<BlockFieldView> Field(string type, string name)
    {
        var catalog = new Catalog(new ScheduleBlock(), new WaitBlock(TimeProvider.System));
        var service = new ListWorkflowBlocksService(catalog, new Docs());
        var listed = await service.Handle(new ListWorkflowBlocksRequest(), Token);
        Assert.True(listed.IsSuccess);
        return listed.Value.Groups.SelectMany(static g => g.Blocks).Single(b => b.Type == type).Config.Single(f => f.Name == name);
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
