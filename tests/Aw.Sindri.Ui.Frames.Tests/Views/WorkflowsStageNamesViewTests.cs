using System.Net;
using System.Text.Json.Nodes;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Tests.Workflows;
using Aw.Sindri.Ui.Views.Workflows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>A board card in a real window says its stage in the recipe's words (what a screen reader hears for the card), once the recipe is read.</summary>
[Lane(TestLane.Integration)]
public sealed class WorkflowsStageNamesViewTests : FrameHost
{
    [Fact]
    public Task A_card_is_named_with_the_recipes_name_for_its_stage() => OnUi(async () =>
    {
        Server.InstancesRewrite = LineGolden.AsLine;
        Server.RecipeBody = id =>
        {
            if (!string.Equals(id, "workflow.line", StringComparison.Ordinal))
            {
                return null;
            }

            var recipe = JsonNode.Parse(GoldenFiles.Api("workflow-recipe", "workflow.project"))!;
            recipe["id"] = id;
            recipe["states"]!.AsArray().Single(s => (string?)s!["id"] == "design")!["displayName"] = "Design the change";
            return new RouteAnswer(HttpStatusCode.OK, recipe.ToJsonString());
        };
        var (window, shell) = await Connected(s => s.Workflows.Lanes.Sum(l => l.Cards.Count) > 0, 1440, 900);
        shell.GoTo(PageId.Workflows);
        Settle(window);

        await Until(window, () => CardNames(window).Exists(n => n.Contains("Design the change", StringComparison.Ordinal)));
        Assert.DoesNotContain(CardNames(window), n => n.Contains(", design,", StringComparison.Ordinal));
    });

    private static List<string> CardNames(Window window) =>
        [.. window.GetVisualDescendants().OfType<RunCard>().Select(c => AutomationProperties.GetName(Named<Border>(c, "WorkflowCardFrame")) ?? string.Empty)];
}
