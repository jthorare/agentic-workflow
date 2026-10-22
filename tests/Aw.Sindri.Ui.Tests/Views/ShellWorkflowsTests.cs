using System.Globalization;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The Workflows page in the window (U8 T2): G W lands on it, the tab counts the open figure, events re-read it, and a re-read never moves what the human holds.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ShellWorkflowsTests : FrameHost
{
    private static void Press(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, keySymbol: null);
        Dispatcher.UIThread.RunJobs();
    }

    private static List<Border> Rows(Window window) =>
        [.. window.GetVisualDescendants().OfType<Border>().Where(b => b.Name == "WorkflowRowFrame" && b.IsEffectivelyVisible)];

    // The fixture's few rows fit the page; copies of them under other addresses make the list taller than the window.
    private static string Padded(string body)
    {
        var table = (JsonObject)JsonNode.Parse(body)!;   // why: the list is a JSON object
        var rows = (JsonArray)table["instances"]!;   // why: it holds the rows
        var copies = Enumerable.Range(0, 4).SelectMany(n => rows.Select(r => CopyOf((JsonObject)r!, n))).ToList();   // why: each row is an object
        foreach (var copy in copies)
        {
            rows.Add(copy);
        }

        return table.ToJsonString();
    }

    private static JsonObject CopyOf(JsonObject row, int n)
    {
        var copy = (JsonObject)row.DeepClone();   // why: an object cloned is an object
        copy["address"] = string.Concat((string?)row["address"], "-pad", n.ToString(CultureInfo.InvariantCulture));
        return copy;
    }

    [Fact]
    public Task G_W_shows_the_Workflows_page_not_the_placeholder() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => s.Workflows.Groups.Count > 0);
        shell.Workflows.SetViewMode(WorkflowViewMode.List);   // the grouped list; the board has its own tests

        Press(window, Key.G);
        Press(window, Key.W);
        Settle(window);

        Assert.Equal(PageId.Workflows, shell.CurrentPage);
        Assert.Equal(("WorkflowsPage", true), (Named<Control>(window, "WorkflowsPage").GetType().Name, Visible(window, "WorkflowsPage")));
        Expect.Sequence(shell.Workflows.Groups.SelectMany(g => g.Rows).Select(r => r.Address), Rows(window).Select(b => ((InstanceRowViewModel)b.DataContext!).Address));
    });

    [Fact]
    public Task Workflow_events_reread_the_list() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => s.Workflows.Groups.Count > 0);
        shell.GoTo(PageId.Workflows);
        shell.Workflows.SetViewMode(WorkflowViewMode.List);   // the grouped list; the board has its own tests
        var reads = shell.Workflows.Drawn;

        Server.Push(A.Event(80, "workflow.started"));

        await Until(window, () => shell.Workflows.Drawn > reads, "the Workflows list was not read again");
    });

    [Fact]
    public Task Refresh_never_moves_focus_scroll_or_input() => OnUi(async () =>
    {
        Server.InstancesRewrite = Padded;
        var (window, shell) = await Connected(s => s.Workflows.Groups.Count > 0, height: 420);
        shell.GoTo(PageId.Workflows);
        shell.Workflows.SetViewMode(WorkflowViewMode.List);   // the grouped list; the board has its own tests
        Settle(window);
        var open = Named<Button>(window, "WorkflowRowOpen");
        Assert.True(open.Focus(NavigationMethod.Tab));
        var filter = Named<TextBox>(window, "SubjectFilter");
        filter.Text = "  ";   // blank: the live search takes it as none, so every row stays and the text must too
        var scroll = Named<ScrollViewer>(window, "Pages");
        scroll.Offset = new Avalonia.Vector(0, 40);
        Settle(window);
        Assert.True(scroll.Offset.Y > 0, $"the list must be scrolled for the check to mean anything (extent {scroll.Extent}, viewport {scroll.Viewport}, rows {shell.Workflows.Groups.Sum(g => g.Rows.Count).ToString(CultureInfo.InvariantCulture)}, rowsDrawn {Rows(window).Count.ToString(CultureInfo.InvariantCulture)})");
        var (focused, offset, reads) = (window.FocusManager?.GetFocusedElement(), scroll.Offset, shell.Workflows.Drawn);

        Server.Push(A.Event(81, "workflow.started"));
        await Until(window, () => shell.Workflows.Drawn > reads, "the Workflows list was not read again");
        Settle(window);

        Assert.Same(focused, window.FocusManager?.GetFocusedElement());
        Assert.Equal(("  ", offset), (Named<TextBox>(window, "SubjectFilter").Text, scroll.Offset));
    });
}
