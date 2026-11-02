using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>U8 key frames (UI spec §18.2) for the children panel, and the way a child opens and returns, against the in-memory API serving the instance goldens. Saved under frames/ as u8-frame-*.png.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ChildrenFramesTests : FrameHost
{
    private async Task<(Window Window, ShellViewModel Shell)> Opened(string scenario)
    {
        var detail = FixtureInstances.Detail(scenario);
        var (window, shell) = await Connected(width: 1600, height: 1000);
        shell.OpenInstance(detail.Address, detail.Id);
        await Until(window, () => shell.Instance.Children is not null && shell.Instance.Address == detail.Address);
        Settle(window);
        OpenDisclosures(window);
        return (window, shell);
    }

    private static void Frame(Window window, string name) => Frame(window, name, "u8-frame-");

    private static List<Button> Buttons(Window window, string name) =>
        [.. window.GetVisualDescendants().OfType<Button>().Where(b => b.Name == name && b.IsEffectivelyVisible)];

    private static List<TextBlock> Texts(Window window, string name) =>
        [.. window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == name && t.IsEffectivelyVisible)];

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [Fact]
    public Task u8_frame_children_workstreams() => OnUi(async () =>
    {
        var (window, _) = await Opened("project-build");
        var served = FixtureInstances.Detail("project-build").Children.Single();

        Assert.Equal(served.Workstreams.Select(w => Labels.StreamTitle(w.Stream, w.Label)), Texts(window, "ChildStreamTitle").Select(t => t.Text), StringComparer.Ordinal);
        Assert.Equal(FixtureInstances.ChildRows(served).Count(), Buttons(window, "ChildRowOpen").Count);
        _ = Assert.Single(Texts(window, "ChildMirrored"));
        Assert.Contains(Texts(window, "ChildGroupHeader"), t => t.Text!.Contains(WorkflowNames.Of(served.Recipe, displayName: null), StringComparison.Ordinal) && !t.Text!.Contains(served.Recipe, StringComparison.Ordinal));
        Named<Control>(window, "ChildrenBlock").BringIntoView();
        var title = Buttons(window, "ChildRowOpen")[0];
        var row = (ChildRowViewModel)title.DataContext!;
        Assert.Equal(Labels.OpenInstanceName(row.Title, row.Address), Heard.Name(title));   // the drawn title leads the name (r3-3)
        Frame(window, "children-workstreams");
    });

    [Fact]
    public Task u8_frame_children_more() => OnUi(async () =>
    {
        var (window, shell) = await Opened("project-many-children");

        Assert.Equal(shell.Instance.Children!.Groups.Single().MoreText, Texts(window, "ChildMore").Single().Text);
        _ = Assert.Single(Buttons(window, "ChildOpenAll"));
        Named<Control>(window, "ChildrenBlock").BringIntoView();
        Frame(window, "children-more");
    });

    [Fact]
    public Task A_child_opened_from_the_frame_is_stacked_and_Back_returns_to_its_parent() => OnUi(async () =>
    {
        var (window, shell) = await Opened("project-build");
        var parent = shell.Instance.Address;
        var child = shell.Instance.Children!.Groups.Single().Streams[0].Rows[0];

        Assert.False(Named<Button>(window, "SheetBack").IsEffectivelyVisible);   // beside the page with no trail there is nothing to go back to
        Click(Buttons(window, "ChildRowOpen")[0]);
        await Until(window, () => shell.Instance.Address == child.Address);
        Assert.Equal((true, child.Id), (shell.IsInstanceSheet, Server.LastInstanceId));
        Assert.True(Named<Button>(window, "SheetBack").IsEffectivelyVisible);   // at 1600 px too: a stacked frame has a way back (L4)

        Click(Named<Button>(window, "SheetBack"));
        await Until(window, () => shell.Instance.Address == parent && shell.Instance.Phase == FramePhase.Shown);

        Assert.True(shell.IsSheetOpen);
        Assert.NotNull(window.FocusManager?.GetFocusedElement());

        // Back to a parent that no longer draws the child (past the "N more" cut) falls back to the frame's first read action (r3-1)
        await Until(window, () => string.Equals((window.FocusManager?.GetFocusedElement() as Button)?.Name, "ChildRowOpen", StringComparison.Ordinal), "focus never returned to the child's row");
        Click((Button)window.FocusManager!.GetFocusedElement()!);
        await Until(window, () => shell.Instance.Address == child.Address, "the child never opened");
        var body = GoldenFiles.Node("workflow-instance", "project-build");
        Without(body["children"]!, child.Address);
        Server.WorkflowAnswer = new RouteAnswer(System.Net.HttpStatusCode.OK, body.ToJsonString());
        Click(Named<Button>(window, "SheetBack"));
        await Until(window, () => shell.Instance.Address == parent && shell.Instance.Phase == FramePhase.Shown, "parent never shown again");
        Assert.DoesNotContain(Buttons(window, "ChildRowOpen"), b => ((ChildRowViewModel)b.DataContext!).Address == child.Address);
        await Until(window, () => window.FocusManager?.GetFocusedElement() is Button { Name: { } name } && (string.Equals(name, "InstanceOpenIn", StringComparison.Ordinal) || string.Equals(name, "ExpandedCopyStatus", StringComparison.Ordinal)), "focus is on the frame's first read action");
    });

    // Drops every object whose address is the given one, wherever it sits under the node.
    private static void Without(System.Text.Json.Nodes.JsonNode node, string address)
    {
        if (node is System.Text.Json.Nodes.JsonArray array)
        {
            for (var i = array.Count - 1; i >= 0; i--)
            {
                if (array[i] is System.Text.Json.Nodes.JsonObject o && string.Equals((string?)o["address"], address, StringComparison.Ordinal))
                {
                    array.RemoveAt(i);
                }
                else
                {
                    Without(array[i]!, address);
                }
            }
        }
        else if (node is System.Text.Json.Nodes.JsonObject obj)
        {
            foreach (var value in obj.Select(p => p.Value).OfType<System.Text.Json.Nodes.JsonNode>().ToList())
            {
                Without(value, address);
            }
        }
    }
}
