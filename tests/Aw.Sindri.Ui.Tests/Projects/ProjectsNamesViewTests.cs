using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>The Projects page in the window (#2805): what a screen reader says for the notes buttons and the folds, and how many cards sit across at 1,280 px.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ProjectsNamesViewTests : FrameHost
{
    [Fact]
    public Task Notes_buttons_have_names_with_project() => OnUi(async () =>
    {
        Server.ProjectsAnswer = RouteAnswer.Api("projects", "mixed");
        var (window, shell) = await Connected();
        shell.GoTo(PageId.Projects);
        await Until(window, () => !shell.Projects.IsLoading, "the Projects page never read the list");
        var card = shell.Projects.Groups[0].Cards[0];
        var frame = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "ProjectCardFrame" && b.DataContext == card);

        Assert.Equal("Edit notes for Checkout rewrite", Heard.Name(frame.GetVisualDescendants().OfType<Button>().First(b => b.Name == "ProjectNotesEdit")));

        card.BeginEdit();
        await Until(window, () => frame.GetVisualDescendants().OfType<Button>().Any(b => b.Name == "ProjectNotesSave" && b.IsEffectivelyVisible), "the editor never showed");
        Assert.EndsWith(" for Checkout rewrite", Heard.Name(frame.GetVisualDescendants().OfType<Button>().First(b => b.Name == "ProjectNotesSave")), StringComparison.Ordinal);
        Assert.EndsWith(" for Checkout rewrite", Heard.Name(frame.GetVisualDescendants().OfType<Button>().First(b => b.Name == "ProjectNotesCancel")), StringComparison.Ordinal);
    });

    // Guard: passes today and must keep passing; the fix sets Name only, so drivers and the perf inventory still find the button by its id.
    [Fact]
    public Task Notes_edit_button_keeps_its_automation_id() => OnUi(async () =>
    {
        Server.ProjectsAnswer = RouteAnswer.Api("projects", "mixed");
        var (window, shell) = await Connected();
        shell.GoTo(PageId.Projects);
        await Until(window, () => !shell.Projects.IsLoading, "the Projects page never read the list");
        var card = shell.Projects.Groups[0].Cards[0];
        var frame = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "ProjectCardFrame" && b.DataContext == card);
        var button = frame.GetVisualDescendants().OfType<Button>().First(b => b.Name == "ProjectNotesEdit");

        Assert.Equal("ProjectNotesEdit", ControlAutomationPeer.CreatePeerForElement(button).GetAutomationId());
    });

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
    public Task SetPageWidth_gives_three_columns_at_1280_window() => OnUi(async () =>
    {
        Server.ProjectsAnswer = RouteAnswer.Api("projects", "mixed");
        var (window, shell) = await Connected(width: 1280, height: 900);
        shell.GoTo(PageId.Projects);
        await Until(window, () => !shell.Projects.IsLoading, "the Projects page never read the list");

        Assert.Equal(3, shell.Projects.Columns);
    });
}
