using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>The Projects page in the window (#2805): what a screen reader says for the folds, and that every column shows at 1,280 px. The card's notes buttons went with the card (issue 3717).</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ProjectsNamesViewTests : FrameHost
{
    // ux rule: a name is the visible label; the toggle already announces expanded or collapsed, so no state word and no Show/Hide verb.
    [Fact]
    public Task Fold_toggle_is_named_by_its_heading() => OnUi(async () =>
    {
        Server.ProjectsAnswer = RouteAnswer.Api("projects", "mixed");
        var (window, shell) = await Connected();
        shell.GoTo(PageId.Projects);
        await Until(window, () => !shell.Projects.IsLoading, "the Projects page never read the list");

        static ToggleButton Toggle(Window window, string fold) =>
            Named<Expander>(window, fold).GetVisualDescendants().OfType<ToggleButton>().First(b => b.Name == "ExpanderHeader");

        Assert.Equal(shell.Projects.Finished.Heading, Heard.Name(Toggle(window, "ProjectsFinished")));
        Assert.Equal(shell.Projects.Hidden.Heading, Heard.Name(Toggle(window, "ProjectsHidden")));

        shell.Projects.Finished.Toggle();
        await Until(window, () => Named<Expander>(window, "ProjectsFinished").IsExpanded, "the fold never opened");

        Assert.Equal(shell.Projects.Finished.Heading, Heard.Name(Toggle(window, "ProjectsFinished")));
    });

    [Fact]
    public Task Every_column_shows_at_a_1280_window() => OnUi(async () =>
    {
        Server.ProjectsAnswer = RouteAnswer.Api("projects", "mixed");
        var (window, shell) = await Connected(width: 1280, height: 900);
        shell.GoTo(PageId.Projects);
        await Until(window, () => !shell.Projects.IsLoading, "the Projects page never read the list");

        Assert.Equal((true, true), (shell.Projects.ShowsAgents, shell.Projects.ShowsWide));
    });
}
