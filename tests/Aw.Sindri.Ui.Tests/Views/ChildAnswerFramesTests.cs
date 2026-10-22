using System.Net;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// "Answer in the child" in the real views, in the headless shell against the in-memory API serving the goldens: the rows of Home and
/// Workflows, the expanded state, following it, and the control's name at the automation peers. Frames are saved as u8-answer-child-*.png
/// when AW_UI_FRAMES=1. The park is the layout golden of the pointer row and the status goldens; nothing is made up in the test.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ChildAnswerFramesTests : FrameHost
{
    private const string Prefix = "u8-answer-child-";
    private const string Detail = "project-build";

    private static JsonNode ListWith(string scenario, bool withChild)
    {
        var list = GoldenFiles.Node("workflow-instances", "root");
        var row = JsonNode.Parse(File.ReadAllText(Path.Combine(GoldenFiles.Root, "Layout", "instance-summary-" + scenario + ".json")))!;
        if (!withChild)
        {
            _ = row["answer"]!.AsObject().Remove("child");
        }

        list["instances"]!.AsArray().Insert(0, row);
        return list;
    }

    private static RouteAnswer Served(JsonNode body) => new(HttpStatusCode.OK, body.ToJsonString());

    private static RouteAnswer DetailWith(string detailScenario, string statusScenario)
    {
        var detail = GoldenFiles.Node("workflow-instance", detailScenario);
        detail["current"]!["status"] = GoldenFiles.Node("workflow-status", statusScenario);
        return Served(detail);
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static List<Button> Pointers(Visual within) =>
        [.. within.GetVisualDescendants().OfType<Button>().Where(b => b.Name == "AnswerInChild" && b.IsEffectivelyVisible)];

    private static string Heard(Control control) => ControlAutomationPeer.CreatePeerForElement(control).GetName();

    [Fact]
    public Task Home_strip_and_the_Workflows_list_show_the_pointer_and_following_it_from_a_row_with_a_child_opens_the_childs_frame() => OnUi(async () =>
    {
        Server.InstancesBody = Served(ListWith("escalated-child", withChild: true));
        var (window, shell) = await Connected(width: 1600, height: 1000, ready: s => s.Strip.Rows.Count > 0);
        var served = FixtureInstances.Summary("escalated-child");
        Frame(window, "home-strip", Prefix);

        var strip = Pointers(window);
        var row = strip[0].GetVisualAncestors().OfType<Border>().First(b => b.Name == "StripRowFrame");

        Assert.Equal(Labels.AnswerInChild, strip[0].Content);
        Assert.StartsWith(Labels.AnswerInChild, Heard(strip[0]), StringComparison.Ordinal);
        Assert.Equal(Labels.AnswerInChildName(served.Answer!.Child!), Heard(strip[0]));
        var shown = row.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible).Select(b => b.Name ?? "").ToList();
        Assert.DoesNotContain("LockButton", shown, StringComparer.Ordinal);
        Assert.DoesNotContain("StripAnswer", shown, StringComparer.Ordinal);

        shell.GoTo(PageId.Workflows);
        shell.Workflows.SetViewMode(WorkflowViewMode.List);   // the grouped list; the board has its own tests
        await Until(window, () => shell.Workflows.Groups.Count > 0 && Pointers(window).Count > 0);
        Frame(window, "workflows-list", Prefix);
        var listed = Pointers(window)[0];
        Assert.StartsWith(Labels.AnswerInChild, Heard(listed), StringComparison.Ordinal);
        Assert.DoesNotContain("LockButton", listed.GetVisualAncestors().OfType<Border>().First(b => b.Name == "WorkflowRowFrame").GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible).Select(b => b.Name ?? ""), StringComparer.Ordinal);

        shell.GoTo(PageId.Home);
        await Until(window, () => Pointers(window).Count > 0);
        Click(Pointers(window)[0]);

        Assert.Equal(new DeepLink(DeepLinkKind.Workflow, served.Answer!.Child), shell.SheetLink);
        Assert.Equal(served.Address, shell.OpenerStrip);
        Frame(window, "strip-follows-to-child", Prefix);
    });

    [Fact]
    public Task Following_it_from_a_row_without_a_child_opens_the_parents_frame_and_a_window_closed_meanwhile_leaves_no_focus_work_queued() => OnUi(async () =>
    {
        Server.InstancesBody = Served(ListWith("escalated-child", withChild: false));
        var (window, shell) = await Connected(width: 1600, height: 1000, ready: s => s.Strip.Rows.Count > 0);
        var served = FixtureInstances.Summary("escalated-child");

        Click(Pointers(window)[0]);

        Assert.Equal(new DeepLink(DeepLinkKind.Workflow, served.Address), shell.SheetLink);

        // The focus the frame asked for is still being chased (no layout pass has run): closing the window now must end the chase (a closed window holds no focus).
        window.Close();
        // The real dispatcher runs the queue while the test awaits; a chase that re-posts itself never lets it drain.
        var drained = false;
        for (var i = 0; i < 100 && !drained; i++)
        {
            await Task.Delay(50, Token);
            drained = !Dispatcher.UIThread.HasJobsWithPriority(DispatcherPriority.Background);
        }

        Assert.True(drained, "the focus chase kept re-posting after the window closed");
    });

    [Fact]
    public Task The_expanded_state_line_follows_the_served_child_by_its_id_or_puts_focus_in_the_Children_panel_when_there_is_none() => OnUi(async () =>
    {
        var withChild = FixtureInstances.Detail(Detail);
        var bare = FixtureInstances.Detail("project-many-children");
        Server.InstanceBody = id =>
            string.Equals(id, withChild.Id, StringComparison.Ordinal) ? DetailWith(Detail, "escalated-child") :
            string.Equals(id, bare.Id, StringComparison.Ordinal) ? DetailWith("project-many-children", "mirrored-budget-no-child") : null;
        var (window, shell) = await Connected(width: 1600, height: 1000);
        var status = FixtureInstances.Status("escalated-child");

        shell.OpenInstance(withChild.Address, withChild.Id);
        await Until(window, () => shell.Instance.Phase == FramePhase.Shown && Visible(window, "ExpandedAnswerInChild"));
        Frame(window, "expanded-with-child", Prefix);
        Assert.StartsWith(Labels.AnswerInChild, Heard(Named<Button>(window, "ExpandedAnswerInChild")), StringComparison.Ordinal);

        Click(Named<Button>(window, "ExpandedAnswerInChild"));

        Assert.Equal(new DeepLink(DeepLinkKind.Workflow, status.Child!.Address), shell.SheetLink);

        shell.OpenInstance(bare.Address, bare.Id);
        await Until(window, () => shell.Instance.Phase == FramePhase.Shown && shell.Instance.Address == bare.Address && Visible(window, "ExpandedAnswerInChild"));
        Frame(window, "expanded-no-child", Prefix);

        Click(Named<Button>(window, "ExpandedAnswerInChild"));

        await Until(window, () => string.Equals((window.FocusManager?.GetFocusedElement() as Button)?.Name, "ChildRowOpen", StringComparison.Ordinal), "focus did not reach the Children panel");
        Assert.Equal(bare.Address, shell.Instance.Address);
        Frame(window, "expanded-no-child-focus", Prefix);
    });
}
