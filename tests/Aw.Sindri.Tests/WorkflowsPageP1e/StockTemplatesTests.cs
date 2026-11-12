using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Templates;
using Aw.Sindri.Core.Graph.Validation;
using Aw.Sindri.Features.ListWorkflowTemplates;
using Aw.Sindri.Tests.Graph.Blocks;
using static Aw.Sindri.Tests.Graph.GraphFixtures;

namespace Aw.Sindri.Tests.WorkflowsPageP1e;

/// <summary>
/// Templates A, B and C (Workflows page spec §4.1, E7, E13, #3193). Readings the spec leaves open, taken closest to its text: A is trigger "PR opened" -> an agent review -> notify (3 steps);
/// B is trigger "issue labelled" -> a Prompt plan -> a comment step (3 steps); C is a nightly schedule -> a check -> If red -> open an issue (4 steps). All need 'connection:github'.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class StockTemplatesTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_templates_service_lists_A_then_B_then_C_with_trigger_step_count_needs_and_no_last_run()
    {
        var result = await new ListWorkflowTemplatesService().Handle(new ListWorkflowTemplatesRequest(), Token);

        Assert.True(result.IsSuccess, result.IsError ? result.Error.ToString() : null);
        var rows = result.Value.Templates;
        Assert.Equal(["A", "B", "C"], rows.Select(static t => t.Id));
        Assert.Equal(["Review every PR", "Plan a labelled issue", "Watch main, open an issue on red"], rows.Select(static t => t.Title));
        Assert.Equal(["github.pr.opened", "github.issue.labeled", "schedule"], rows.Select(static t => t.Trigger));
        Assert.Equal([3, 3, 4], rows.Select(static t => t.Steps));
        Assert.All(rows, t =>
        {
            Assert.Equal(["connection:github"], t.Needs);
            Assert.Null(t.LastRunAt);
        });
    }

    [Theory]
    [InlineData("A")]
    [InlineData("B")]
    [InlineData("C")]
    public void A_template_has_one_trigger_and_every_node_type_is_a_block_the_catalog_knows(string id)
    {
        var template = StockTemplates.Find(id);

        Assert.NotNull(template);
        var catalog = GraphBlockRegistry.Standard(new BlockFakes.RecordingNotifyPort(), new BlockFakes.ScriptedModelPort("{}")).Types;
        Assert.Equal(1, template.Spec.Nodes.Count(n => catalog[n.Type].IsTrigger));
        Assert.All(template.Spec.Nodes, n => Assert.True(catalog.ContainsKey(n.Type), $"template {id} uses unknown block '{n.Type}'"));
    }

    [Theory]
    [InlineData("A")]
    [InlineData("B")]
    [InlineData("C")]
    public void A_template_validates_clean_as_a_draft_with_the_blocks_it_needs(string id)
    {
        var template = StockTemplates.Find(id);

        Assert.NotNull(template);
        var catalog = GraphBlockRegistry.Standard(new BlockFakes.RecordingNotifyPort(), new BlockFakes.ScriptedModelPort("{}")).Types;
        var validation = Validator(blocks: catalog).Validate(template.Spec, GraphMode.Draft);

        Assert.True(validation.Passed, string.Join("; ", validation.Blocking.Select(static f => f.ToString())));
        Assert.DoesNotContain(validation.Findings, static f => f.Code.StartsWith("GRAPH-BLOCK-", StringComparison.Ordinal));
    }
}
