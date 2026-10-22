using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The instance frame in the window (U8 T4): a workflow link and the strip's Enter open it in the sheet at the widest width, the list behind it reflows, and the frame outlives the instance it shows.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class ShellInstanceTests : FrameHost
{
    private static string Address(string scenario = "project-build") => (string)GoldenFiles.Node("workflow-instance", scenario)["address"]!;

    private static void Press(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, keySymbol: null);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public Task Deep_link_workflow_address_opens_the_frame() => OnUi(async () =>
    {
        var (window, shell) = await Connected();

        shell.Navigate(new DeepLink(DeepLinkKind.Workflow, Address()));
        await Until(window, () => shell.Instance.Phase == FramePhase.Shown);

        Assert.Equal((true, false, "Workflow " + Address()), (shell.IsInstanceSheet, shell.IsSheetPlaceholderShown, shell.SheetName));
        Assert.Equal((true, false), (Visible(window, "InstanceFrame"), Visible(window, "SheetBody")));
        var title = Named<TextBlock>(window, "SheetSubjectText");   // a long address in the 760 px sheet is trimmed, never cut off unseen: the whole address is the tooltip
        Assert.Equal((Avalonia.Media.TextTrimming.CharacterEllipsis, Address()), (title.TextTrimming, ToolTip.GetTip(title)));
        Assert.Equal((string)GoldenFiles.Node("workflow-instance", "project-build")["title"]!, Named<TextBlock>(window, "InstanceTitle").Text);
    });

    [Fact]
    public Task Strip_Enter_opens_the_frame_not_the_placeholder() => OnUi(async () =>
    {
        var served = FixtureInstances.Details.Select(d => d.Detail.Id).ToList();
        var (window, shell) = await Connected(s => s.Strip.Rows.Any(r => served.Contains(r.InstanceId)));
        var row = shell.Strip.Rows.First(r => served.Contains(r.InstanceId));
        shell.SelectStripRow(row.Address);

        Press(window, Key.Enter);
        await Until(window, () => shell.Instance.Phase == FramePhase.Shown);

        Assert.Equal((true, false, row.InstanceId, row.Address), (shell.IsInstanceSheet, shell.IsSheetPlaceholderShown, Server.LastInstanceId, shell.Instance.Address));
        Assert.True(Visible(window, "InstanceFrame"));
    });

    [Fact]
    public Task Instance_sheets_open_at_760_and_keep_the_users_width_for_other_sheets() => OnUi(async () =>
    {
        var (window, shell) = await Connected(width: 1600, height: 900);
        var kept = shell.SheetWidth;

        shell.Navigate(new DeepLink(DeepLinkKind.Workflow, Address()));
        await Until(window, () => shell.Instance.Phase == FramePhase.Shown);
        var instance = Named<Border>(window, "Sheet").Width;
        shell.Navigate(new DeepLink(DeepLinkKind.Item, FixtureInstances.Detail("done").Facts.First(f => f.Type == Ui.Api.Workflow.FactType.Item).Ref!.ItemId!));
        Settle(window);

        Assert.Equal((UiPreferences.MaxSheetWidth, kept, kept), (instance, Named<Border>(window, "Sheet").Width, shell.SheetWidth));
    });

    [Fact]
    public Task Behind_a_sheet_the_list_drops_its_Stage_and_Waiting_on_columns() => OnUi(async () =>
    {
        var (window, shell) = await Connected(s => s.Workflows.Groups.Count > 0, width: 1300, height: 900);
        shell.GoTo(PageId.Workflows);
        shell.Workflows.SetViewMode(WorkflowViewMode.List);
        Settle(window);
        bool Shown(string name) => window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Name == name && t.IsEffectivelyVisible);
        Assert.Equal((true, true, true), (Shown("WorkflowRowStage"), Shown("WorkflowRowNow"), Shown("WorkflowRowReference")));   // a wide page keeps all six columns

        shell.Navigate(new DeepLink(DeepLinkKind.Workflow, Address()));
        await Until(window, () => shell.Instance.Phase == FramePhase.Shown);

        var row = shell.Workflows.Groups.SelectMany(g => g.Rows).First();
        Assert.Equal((false, false), (Shown("WorkflowRowStage"), Shown("WorkflowRowWho")));   // behind the sheet the page is under 960 wide: Stage and Waiting on go
        Assert.Equal(row.Subject, ((InstanceRowViewModel)window.GetVisualDescendants().OfType<Button>().First(b => b.Name == "WorkflowRowOpen" && b.IsEffectivelyVisible).DataContext!).Subject);
    });

    [Fact]
    public Task A_frame_whose_instance_ends_keeps_its_content_and_says_done() => OnUi(async () =>
    {
        var (window, shell) = await Connected();
        shell.Navigate(new DeepLink(DeepLinkKind.Workflow, Address()));
        await Until(window, () => shell.Instance.Phase == FramePhase.Shown);
        var reads = shell.Health.ViewReads;

        Server.WorkflowAnswer = RouteAnswer.Api("workflow-instance", "done");
        Server.Push(A.Event(90, "workflow.completed"));
        await Until(window, () => shell.Health.ViewReads > reads && shell.Instance.Note.Length > 0, "the frame was not read again");

        Assert.Equal((true, Labels.InstanceDone), (Visible(window, "InstanceNote"), Named<TextBlock>(window, "InstanceNote").Text));
        Assert.True(Visible(window, "InstanceBand"));
        Assert.True(shell.IsSheetShown);
    });

    [Fact]
    public Task Esc_returns_focus_to_the_Workflows_row_that_opened_the_frame() => OnUi(async () =>
    {
        var served = FixtureInstances.Details.Select(d => d.Detail.Address).ToList();
        var (window, shell) = await Connected(s => s.Workflows.Groups.Count > 0);
        shell.GoTo(PageId.Workflows);
        shell.Workflows.SetViewMode(WorkflowViewMode.List);   // the grouped list; the board has its own tests
        Settle(window);
        var open = window.GetVisualDescendants().OfType<Button>().First(b => b.Name == "WorkflowRowOpen" && b.DataContext is InstanceRowViewModel r && served.Contains(r.Address));
        Assert.True(open.Focus(NavigationMethod.Tab));

        shell.Workflows.Open(((InstanceRowViewModel)open.DataContext!).Address);
        await Until(window, () => shell.Instance.Phase == FramePhase.Shown);
        shell.CloseSheet();
        await Until(window, () => ReferenceEquals(window.FocusManager?.GetFocusedElement(), open), "focus did not return to the row that opened the frame");
    });
}
