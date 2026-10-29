using System.Text.Json.Nodes;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Projects;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Shell;

namespace Aw.Sindri.Ui.Tests.Projects;

/// <summary>The Projects page by keyboard (projects page spec §3.3): the card grid is one tab stop, arrows move by card in reading order, the folds are their own stops.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ProjectsGridKeysTests : FrameHost
{
    // Three across: In progress holds A B C and D (a short second row), then Planned and Paused hold one each, so the rows are [A B C] [D] [E] [F].
    private async Task<(ShellWindow Window, ShellViewModel Shell)> OnProjects(bool finishedOpen = false)
    {
        var list = ProjectsFixture.Golden("mixed");
        var projects = list["projects"]!.AsArray();
        var first = projects[0]!;
        projects.Insert(0, Copy(first, "A", "linear:project/a"));
        projects.Insert(1, Copy(first, "B", "linear:project/b"));
        projects.Insert(2, Copy(first, "C", "linear:project/c"));
        projects.Insert(3, Copy(first, "D", "linear:project/d"));
        projects.RemoveAt(4);
        projects[4]!["name"] = "E";
        projects[5]!["name"] = "F";
        list["finished"]![0]!["name"] = "G";
        list["hidden"]![0]!["name"] = "H";
        Server.ProjectsAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, list.ToJsonString());

        var (window, shell) = await Connected(width: 1600);
        shell.GoTo(PageId.Projects);
        await Until(window, () => !shell.Projects.IsLoading && shell.Projects.Columns == 3, "the Projects page never drew three across");
        if (finishedOpen)
        {
            shell.Projects.Finished.Toggle();
            await Until(window, () => Frames(window).Count == 7, "the Finished card never showed");
        }

        return (window, shell);
    }

    private static JsonNode Copy(JsonNode source, string name, string id)
    {
        var copy = JsonNode.Parse(source.ToJsonString())!;
        copy["name"] = name;
        copy["id"] = id;
        return copy;
    }

    private static List<Border> Frames(Window window) =>
        [.. window.GetVisualDescendants().OfType<Border>().Where(b => b.Name == "ProjectCardFrame" && b.IsEffectivelyVisible)];

    private static string? Focused(Window window) => window.FocusManager?.GetFocusedElement() switch
    {
        Border { DataContext: ProjectCardViewModel card } => card.Name,
        Control control => control.Name,
        _ => null,
    };

    private static void Press(Window window, PhysicalKey key)
    {
        window.KeyPressQwerty(key, RawInputModifiers.None);
        Settle(window);
    }

    private static void FocusCard(Window window, string name) =>
        Assert.True(Frames(window).First(f => ((ProjectCardViewModel)f.DataContext!).Name == name).Focus(NavigationMethod.Tab));

    private static string?[] Move(Window window, string from, PhysicalKey key)
    {
        FocusCard(window, from);
        Press(window, key);
        return [from, Focused(window)];
    }

    [Fact]
    public Task The_card_grid_is_one_tab_stop() => OnUi(async () =>
    {
        var (window, _) = await OnProjects();

        Assert.Equal(1, Frames(window).Count(f => f.IsTabStop));

        FocusCard(window, "C");
        Settle(window);

        Assert.Equal(["C"], Frames(window).Where(f => f.IsTabStop).Select(f => ((ProjectCardViewModel)f.DataContext!).Name), StringComparer.Ordinal);
    });

    [Fact]
    public Task Right_and_Left_move_by_card_in_reading_order_across_rows_and_groups() => OnUi(async () =>
    {
        var (window, _) = await OnProjects();

        Assert.Equal(["A", "B"], Move(window, "A", PhysicalKey.ArrowRight));
        Assert.Equal(["C", "D"], Move(window, "C", PhysicalKey.ArrowRight));
        Assert.Equal(["D", "E"], Move(window, "D", PhysicalKey.ArrowRight));
        Assert.Equal(["D", "C"], Move(window, "D", PhysicalKey.ArrowLeft));
        Assert.Equal(["A", "A"], Move(window, "A", PhysicalKey.ArrowLeft));
    });

    [Fact]
    public Task Down_and_Up_keep_the_column_and_a_shorter_row_gives_its_last_card() => OnUi(async () =>
    {
        var (window, _) = await OnProjects();

        Assert.Equal(["A", "D"], Move(window, "A", PhysicalKey.ArrowDown));
        Assert.Equal(["C", "D"], Move(window, "C", PhysicalKey.ArrowDown));
        Assert.Equal(["D", "A"], Move(window, "D", PhysicalKey.ArrowUp));
        Assert.Equal(["D", "E"], Move(window, "D", PhysicalKey.ArrowDown));
        Assert.Equal(["E", "D"], Move(window, "E", PhysicalKey.ArrowUp));
        Assert.Equal(["A", "A"], Move(window, "A", PhysicalKey.ArrowUp));
    });

    [Fact]
    public Task Arrows_inside_a_text_box_are_left_to_it() => OnUi(async () =>
    {
        var (window, _) = await OnProjects();
        var frame = Frames(window).First(f => ((ProjectCardViewModel)f.DataContext!).Name == "B");
        var box = new TextBox { Text = "note" };
        ((Panel)frame.Parent!).Children.Add(box); // why: stands in for the notes box another change adds to a card
        Settle(window);
        Assert.True(box.Focus(NavigationMethod.Tab));

        Press(window, PhysicalKey.ArrowRight);
        Press(window, PhysicalKey.ArrowDown);

        Assert.Same(box, window.FocusManager?.GetFocusedElement());
    });

    [Fact]
    public Task Enter_on_a_card_is_not_taken_by_the_grid() => OnUi(async () =>
    {
        var (window, _) = await OnProjects();
        FocusCard(window, "B");

        var seen = false;
        window.AddHandler(InputElement.KeyDownEvent, (_, e) => seen |= e.Key == Key.Enter && !e.Handled, Avalonia.Interactivity.RoutingStrategies.Bubble);
        Press(window, PhysicalKey.Enter);

        Assert.True(seen);
    });

    [Fact]
    public Task A_fold_is_a_disclosure_that_Enter_and_Space_toggle_and_focus_stays_on_its_button() => OnUi(async () =>
    {
        var (window, shell) = await OnProjects();
        var fold = Named<Expander>(window, "ProjectsFinished");
        var button = fold.GetVisualDescendants().OfType<ToggleButton>().First();

        Assert.True(button.Focus(NavigationMethod.Tab));
        Assert.Equal(ExpandCollapseState.Collapsed, State(fold));

        Press(window, PhysicalKey.Enter);
        Assert.Equal((true, ExpandCollapseState.Expanded, true), (shell.Projects.Finished.IsOpen, State(fold), button.IsFocused));

        Press(window, PhysicalKey.Space);
        Assert.Equal((false, ExpandCollapseState.Collapsed, true), (shell.Projects.Finished.IsOpen, State(fold), button.IsFocused));
    });

    [Fact]
    public Task An_open_fold_has_its_own_grid_stop_after_its_button_and_arrows_stay_inside_it() => OnUi(async () =>
    {
        var (window, _) = await OnProjects(finishedOpen: true);
        var inFold = Frames(window).First(f => ((ProjectCardViewModel)f.DataContext!).Name == "G");
        var stops = Frames(window).Where(f => f.IsTabStop).Select(f => ((ProjectCardViewModel)f.DataContext!).Name).ToList();

        Assert.Equal(2, stops.Count);
        Assert.Contains("G", stops, StringComparer.Ordinal);

        Assert.True(inFold.Focus(NavigationMethod.Tab));
        Press(window, PhysicalKey.ArrowRight);
        Press(window, PhysicalKey.ArrowDown);
        Press(window, PhysicalKey.ArrowUp);
        Assert.Equal("G", Focused(window));

        var button = Named<Expander>(window, "ProjectsFinished").GetVisualDescendants().OfType<ToggleButton>().First();
        Assert.True(button.Focus(NavigationMethod.Tab));
        Press(window, PhysicalKey.ArrowDown);
        Assert.Same(button, window.FocusManager?.GetFocusedElement());
    });

    private static ExpandCollapseState State(Expander fold) =>
        ControlAutomationPeer.CreatePeerForElement(fold).GetProvider<IExpandCollapseProvider>()!.ExpandCollapseState; // why: an Expander always has the provider
}
