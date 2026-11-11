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
        Assert.Equal("duration", json["caption"]!.GetValue<string>());   // no caption set: the name stands in
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
