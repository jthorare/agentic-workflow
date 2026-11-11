using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Docs;
using Aw.Sindri.Features.ListWorkflowBlocks;

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
    public async Task Schedule_timezone_field_offers_zone_ids_labelled_by_the_id()
    {
        var timezone = await Field("schedule", "timezone");

        Assert.Equal("Timezone", timezone.Caption);
        Assert.Contains(timezone.Choices!, static c => c.Value == "America/Los_Angeles" && c.Label == "America/Los_Angeles");
        Assert.Null(timezone.Presets);
    }

    [Fact]
    public async Task A_field_with_no_lists_leaves_choices_and_presets_out_of_the_json()
    {
        var plain = await Field("schedule", "cron", withLists: false);

        var json = JsonSerializer.SerializeToNode(plain, _web)!.AsObject();

        Assert.False(json.ContainsKey("choices"));
        Assert.False(json.ContainsKey("presets"));
        Assert.Equal("Runs at", json["caption"]!.GetValue<string>());
    }

    private static async Task<BlockFieldView> Field(string type, string name, bool withLists = true)
    {
        var block = new ScheduleBlock();
        var catalog = new Catalog(block);
        var service = new ListWorkflowBlocksService(catalog, new Docs());
        var listed = await service.Handle(new ListWorkflowBlocksRequest(), Token);
        Assert.True(listed.IsSuccess);
        var field = listed.Value.Groups.SelectMany(static g => g.Blocks).Single(b => b.Type == type).Config.Single(f => f.Name == name);
        // why: the third test needs a view that has neither list, which the real field has once the lists are dropped
        return withLists ? field : field with { Choices = null, Presets = null };
    }

    private sealed class Catalog(IGraphBlockType block) : IGraphBlockCatalog
    {
        public IReadOnlyDictionary<string, IGraphBlockType> Types { get; } = new Dictionary<string, IGraphBlockType>(StringComparer.Ordinal) { [block.Type] = block };
    }

    private sealed class Docs : IWorkflowDocs
    {
        public IReadOnlyList<WorkflowDocPage> Pages => [];
    }
}
