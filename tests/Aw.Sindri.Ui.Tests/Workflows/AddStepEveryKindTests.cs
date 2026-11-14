using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using static Aw.Sindri.Ui.Tests.Workflows.WorkflowsFixture;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>
/// #3072: for every block the Add step menu offers (the API's own workflows-blocks golden), Add step then Save with nothing touched sends one add_node whose config is the block's example, unchanged.
/// The engine side (that each example passes GRAPH-CONFIG-001) is BlockExamplesAcceptedTests in Aw.Sindri.Tests: the Ui project has no reference to the engine's validator.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class AddStepEveryKindTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string> Kinds() =>
        [.. Golden("workflows-blocks", "listed")["groups"]!.AsArray().SelectMany(static g => g!["blocks"]!.AsArray()).Select(static b => b!["type"]!.GetValue<string>())];

    [Theory]
    [MemberData(nameof(Kinds))]
    public async Task Save_with_the_example_untouched_sends_that_example_as_the_new_steps_settings(string type)
    {
        var f = new WorkflowsFixture();
        f.Server.WorkflowBlocksAnswer = RouteAnswer.Api("workflows-blocks", "listed");
        var page = await f.Shown(Token);
        await page.OpenWorkflow(Slug, Token);
        await page.GraphEdit.BeginAdd(Token);
        var block = page.GraphEdit.Groups.SelectMany(static g => g.Blocks).Single(b => b.Type == type);

        await page.GraphEdit.AddStep(block, Token);
        Assert.True(page.CanSave, $"'{type}': Save is off with the example untouched; empty required: " + string.Join(", ", page.Editor.Fields.Where(static x => x.IsRequired && string.IsNullOrWhiteSpace(x.Text)).Select(static x => x.Name)));
        await page.Save(Token);

        var sent = JsonNode.Parse(Assert.Single(f.Server.AppliedWorkflowOps).Ops)!.AsArray()[0]!;
        Assert.Equal(type, sent["type"]!.GetValue<string>());
        var example = block.ExampleConfig is { } raw ? JsonNode.Parse(raw.GetRawText()) as JsonObject ?? [] : [];
        Assert.True(JsonNode.DeepEquals(example, sent["config"] ?? new JsonObject()), $"'{type}': sent {sent["config"]} but the example is {example}");
    }
}
