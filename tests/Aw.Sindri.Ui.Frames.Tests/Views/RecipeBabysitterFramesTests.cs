using Avalonia.Controls;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The recipe sheet's Babysitter section (api #778) in its window: it shows below the run figures when the stats carry the babysitter's figures, and not otherwise.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class RecipeBabysitterFramesTests : FrameHost
{
    [Theory]
    [InlineData("pr-babysitter", true)]
    [InlineData("workflow-item", false)]
    public Task The_section_shows_only_with_the_babysitters_figures(string scenario, bool shown) => OnUi(async () =>
    {
        Server.RecipeStatsAnswer = RouteAnswer.Api("workflow-recipes-id-stats", scenario);
        var (window, shell) = await Connected(width: 1600, height: 1000);

        shell.Navigate(new DeepLink(DeepLinkKind.Recipe, "workflow.dev-triage-three-state"));
        await Until(window, () => shell.Recipe.Phase == FramePhase.Shown && shell.IsRecipeSheet && shell.Recipe.HasBabysitter == shown);
        Settle(window);

        Assert.Equal(shown, Visible(window, "RecipeBabysitter"));
        if (shown)
        {
            Assert.Equal("Babysitter", Named<TextBlock>(window, "RecipeBabysitterHeading").Text);
            var texts = Named<StackPanel>(window, "RecipeBabysitter").GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).Select(t => t.Text).ToList();
            Assert.Contains(texts, t => string.Equals(t, "typical 1h 30m, most under 4h 0m", StringComparison.Ordinal));
            Assert.Contains(texts, t => string.Equals(t, "1 merge record couldn't be read", StringComparison.Ordinal));
            Frame(window, "recipe-babysitter", "f3-");
        }
    });
}
