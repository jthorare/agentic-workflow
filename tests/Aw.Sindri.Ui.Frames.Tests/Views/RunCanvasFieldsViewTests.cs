using System.Net;
using System.Text.Json;
using Avalonia.Controls;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.ViewModels.Run;
using Aw.Sindri.Ui.Views.Run;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The run canvas in a real window with the engine's recipe fields (F5 A fields): the one-line note that branches are not served goes when the recipe serves its transitions, and a card names who does its state.</summary>
[Lane(TestLane.Integration)]
public sealed class RunCanvasFieldsViewTests : FrameHost
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static RouteAnswer Body<T>(T value) => new(HttpStatusCode.OK, JsonSerializer.Serialize(value, SindriApiClient.Json));

    private async Task<(Window Window, RunPageViewModel Page)> Shown(RecipeView recipe)
    {
        var line = RunFixtures.Line("plan");
        Server.InstanceBody = id => string.Equals(id, line.Id, StringComparison.Ordinal) ? Body(line)
            : Body(RunFixtures.Child(id["WFI-".Length..], ("produce", StateGlyph.Done, false)));
        Server.RecipeBody = _ => Body(recipe);
        var page = new RunPageViewModel(Server, new CatalogViewModel(new FakeClipboard(), new FakeTimeProvider(A.Noon)), new FakeClipboard(), new FakeTimeProvider(A.Noon));
        await page.Open(_api, line.Address, line.Id, Token);
        var window = new Window { Width = 1400, Height = 900, Content = new RunPage { DataContext = page } };
        window.Show();
        Settle(window);
        return (window, page);
    }

    [Fact]
    public Task Served_transitions_take_the_not_served_note_away() => OnUi(async () =>
    {
        var recipe = RunFixtures.LineRecipe() with
        {
            Transitions = [new RecipeTransitionView(["scoping"], "step.completed", "design", "done"), new RecipeTransitionView(["plan"], "step.completed", "design", "failed", "problems found: design it again")],
        };
        var (window, page) = await Shown(recipe);

        Assert.True(page.IsShown);
        Assert.False(Visible(window, "RunBranchesNote"));
        window.Close();
    });

    [Fact]
    public Task A_recipe_without_transitions_keeps_the_note() => OnUi(async () =>
    {
        var (window, page) = await Shown(RunFixtures.LineRecipe());

        Assert.True(page.IsShown);
        Assert.True(Visible(window, "RunBranchesNote"));
        window.Close();
    });

    [Fact]
    public Task The_pill_of_a_served_label_sits_on_its_edge() => OnUi(async () =>
    {
        var recipe = RunFixtures.LineRecipe() with
        {
            Transitions = [new RecipeTransitionView(["scoping"], "step.completed", "design", "done"), new RecipeTransitionView(["plan"], "step.completed", "design", "failed", "problems found: design it again")],
        };
        var (window, page) = await Shown(recipe);

        Assert.Contains(page.Pills, p => string.Equals(p.Text, "problems found: design it again", StringComparison.Ordinal));
        window.Close();
    });
}
