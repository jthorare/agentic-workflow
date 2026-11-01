using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Recipes;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>U10a key frames for the recipe sheet (UI spec §18.2), against the in-memory API serving the recipe goldens. Saved under frames/ as u10a-recipe-*.png.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class RecipeFramesTests : FrameHost
{
    private const string Prefix = "u10a-recipe-";

    private static void Frame(Window window, string name) => Frame(window, name, Prefix);

    private static async Task Open(Window window, ShellViewModel shell, string subject, FramePhase phase = FramePhase.Shown)
    {
        shell.Navigate(new DeepLink(DeepLinkKind.Recipe, subject));
        await Until(window, () => shell.Recipe.Phase == phase && shell.IsRecipeSheet);
        Settle(window);
    }

    private static List<(string Name, AutomationControlType Type)> Tree(Window window) => Heard.Tree(Named<Control>(window, "RecipeSheet"));

    private static void AssertReached(Window window, RecipeSheetViewModel recipe)
    {
        var heard = Tree(window);
        var expected = recipe.States!.Nodes.Concat(recipe.States.Nodes.SelectMany(n => n.Branches)).Select(n => n.Name)
            .Concat(recipe.Facts!.Inputs.Select(i => i.Label)).Concat(recipe.Facts.Outputs.Select(o => o.Label));
        Assert.All(expected, name => Assert.Contains((name, AutomationControlType.Text), heard));
        Assert.All(Heard.Tree(Named<Control>(window, "RecipeStates")).Where(node => node.Type == AutomationControlType.Text), node => Assert.False(string.IsNullOrEmpty(node.Name), "a nameless text node in the band"));   // a live accessibility dump showed a bare TextBlock after the last state
        Assert.DoesNotContain(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Name == "RecipeStateLink" && t.IsEffectivelyVisible && string.IsNullOrEmpty(t.Text));   // an empty link is not drawn at all
        Assert.Equal(recipe.Title, Named<TextBlock>(window, "RecipeTitle").Text);   // the sheet leads with the workflow's name; the id@hash12 waits in Details
    }

    [Fact]
    public Task u10a_recipe_shown() => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: 1600, height: 1000);

        // desk-round-trip: a loop between neighbours, with its cap
        await Open(window, shell, "workflow.dev-desk-round-trip");
        shell.Recipe.ToggleDetails();   // the identity line sits in Details
        Settle(window);
        Assert.Equal(shell.Recipe.Identity, Named<TextBlock>(window, "RecipeIdentity").Text);
        Assert.Contains(Named<TextBlock>(window, "RecipeStateLink").Text, shell.Recipe.States!.Nodes.Select(n => n.Link), StringComparer.Ordinal);
        Assert.Equal(shell.Recipe.States.Nodes.Count, window.GetVisualDescendants().OfType<TextBlock>().Count(t => t.Name == "RecipeStateText" && t.IsEffectivelyVisible));
        AssertReached(window, shell.Recipe);
        Frame(window, "desk-round-trip");

        // fork-demo: a branch folds under its source, and the band says it is not a path
        await Open(window, shell, "workflow.fork-demo");
        Assert.True(Visible(window, "RecipeOrderNote"));
        AssertReached(window, shell.Recipe);
        Frame(window, "fork-demo");

        // project: a loop into the same state is a note, outputs sit under their states, an input has a payload kind
        await Open(window, shell, "workflow.project@" + FixtureInstances.Recipe("workflow.project").Hash["sha256:".Length..][..12]);
        Assert.True(Visible(window, "RecipeStateNote"));
        Assert.True(Visible(window, "RecipeStateOutput"));
        AssertReached(window, shell.Recipe);
        Frame(window, "project");
    });

    [Fact]
    public Task u10a_recipe_not_found() => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: 1600, height: 1000);

        await Open(window, shell, "workflow.unknown", FramePhase.NotFound);

        Assert.Equal(shell.Recipe.ProblemText, Named<TextBlock>(window, "RecipeProblem").Text);
        Assert.True(Visible(window, "SheetBack"));   // the sheet covers the page, so the header's Back is the one (the body's hides)
        Frame(window, "not-found");
    });
}
