using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Infrastructure.WorkflowDocs;

namespace Aw.Sindri.Tests.Graph.Blocks;

[Lane(TestLane.Unit)]
public sealed class GraphBlockListingTests
{
    private static IReadOnlyList<GraphBlockGroup> Listing() =>
        GraphBlockListing.Group(GraphBlockRegistry.Standard(new BlockFakes.RecordingNotifyPort(), new BlockFakes.ScriptedModelPort("{}")), EmbeddedWorkflowDocs.Load());

    [Fact]
    public void Blocks_are_grouped_Triggers_before_Data_before_AI_before_Logic_before_Outputs()
    {
        var groups = Listing().Select(static g => g.Group).ToList();

        Assert.Equal([BlockGroup.Triggers, BlockGroup.AI, BlockGroup.Logic, BlockGroup.Outputs], groups);
        Assert.Equal(groups.Order(), groups);
        Assert.True(BlockGroup.Triggers < BlockGroup.Data && BlockGroup.Data < BlockGroup.AI && BlockGroup.AI < BlockGroup.Logic && BlockGroup.Logic < BlockGroup.Outputs);
    }

    [Fact]
    public void Each_group_orders_its_blocks_by_title_and_shows_the_picker_label()
    {
        var triggers = Listing().Single(static g => g.Group == BlockGroup.Triggers);

        Assert.Equal(["Connection event", "Schedule", "When called"], triggers.Blocks.Select(static b => b.Title), StringComparer.Ordinal);
        Assert.Equal(["connection_event", "schedule", "manual"], triggers.Blocks.Select(static b => b.Type), StringComparer.Ordinal);
    }

    [Fact]
    public void An_entry_carries_the_blocks_schema_outputs_and_the_example_spec_of_its_page()
    {
        var schedule = Listing().SelectMany(static g => g.Blocks).Single(static b => string.Equals(b.Type, "schedule", StringComparison.Ordinal));

        Assert.True(schedule.IsTrigger);
        Assert.Equal(["cron", "timezone"], schedule.Config.Fields.Select(static f => f.Name), StringComparer.Ordinal);
        Assert.Equal(["scheduledFor", "localDate", "weekday"], schedule.Outputs.Fields.Keys, StringComparer.Ordinal);
        Assert.Contains("\"schedule\"", schedule.ExampleSpec, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_block_page_is_titled_with_the_block_title()
    {
        var pages = EmbeddedWorkflowDocs.Load();

        foreach (var block in Listing().SelectMany(static g => g.Blocks))
        {
            Assert.Equal(block.Title, Assert.Single(pages, p => string.Equals(p.BlockType, block.Type, StringComparison.Ordinal)).Title);
        }
    }
}
