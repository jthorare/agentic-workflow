using System.Net;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Facts;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Aw.Sindri.Ui.Views.Components;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>The Workflows keys in the window (U8 T7, UI spec §8, §11.2): J / K carry focus with the selection, a refresh never moves it, a row that goes leaves focus somewhere, and each key lands focus where it acts. Frames saved as u8-keys-*.png.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class WorkflowsKeysViewTests : FrameHost
{
    private static readonly Uri _api = new("http://127.0.0.1:7410/");

    private static IReadOnlyList<InstanceSummary> Served => FixtureInstances.List("open").Instances;

    private static InstanceSummary Desk => Served.First(r => r.Answer is null && r.Actions.Contains("CompleteStep", StringComparer.Ordinal) && r.States.Any(s => s.Desk && string.Equals(s.State, r.State, StringComparison.Ordinal)));

    private static InstanceSummary Locked => Served.First(r => r.Answer is not null && string.Equals(r.Answer.Class, "protected", StringComparison.Ordinal));

    private static InstanceSummary Plain => Served.First(r => r.Answer is null && r.Actions.Contains("ApplyCommand:cancel", StringComparer.Ordinal) && !r.Actions.Contains("CompleteStep", StringComparer.Ordinal));

    private static void Press(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, keySymbol: null);
        Dispatcher.UIThread.RunJobs();
    }

    private async Task<(Window Window, ShellViewModel Shell)> OnWorkflows()
    {
        var (window, shell) = await Connected(s => s.Workflows.Groups.Count > 0);
        shell.GoTo(PageId.Workflows);
        shell.Workflows.SetViewMode(WorkflowViewMode.List);   // the grouped list; the board has its own tests
        Settle(window);
        _ = window.Get<Control>("Page").Focus(NavigationMethod.Tab);
        return (window, shell);
    }

    private static Border RowFrame(Window window, string address) =>
        window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "WorkflowRowFrame" && b.IsEffectivelyVisible && ((InstanceRowViewModel)b.DataContext!).Address == address);

    // True while focus is on the address's row frame, also while a re-read has not yet drawn the row.
    private static bool FocusIsOn(Window window, string address) =>
        Focused(window) is Border { IsEffectivelyVisible: true, DataContext: InstanceRowViewModel row } frame && string.Equals(frame.Name, "WorkflowRowFrame", StringComparison.Ordinal) && string.Equals(row.Address, address, StringComparison.Ordinal);

    // Waits for a state; when it never comes, the failure says where focus is and what the frame holds.
    private static async Task Reach(Window window, ShellViewModel shell, Func<bool> condition)
    {
        try
        {
            await Until(window, condition);
        }
        catch (Xunit.Sdk.XunitException e)
        {
            throw new InvalidOperationException(Describe(window, shell), e);
        }
    }

    private static string Describe(Window window, ShellViewModel shell) =>
        $"{Focused(window)?.GetType().Name}/{(Focused(window) as Control)?.Name} sheet={shell.IsSheetOpen} phase={shell.Instance.Phase} kbd={shell.Instance.Expanded?.IsConfirmingCancel} copyButtons={window.GetVisualDescendants().OfType<Button>().Count(b => b.Name == "FactCopyCli")}/{window.GetVisualDescendants().OfType<Button>().Count(b => b.Name == "FactCopyCli" && b.IsEffectivelyVisible)}";

    private static object? Focused(Window window) => window.FocusManager?.GetFocusedElement();

    [Fact]
    public Task J_carries_focus_onto_the_selected_row_across_groups() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows();
        var flat = shell.Workflows.Groups.SelectMany(g => g.Rows).Select(r => r.Address).ToList();

        foreach (var address in flat)
        {
            Press(window, Key.J);
            await Until(window, () => ReferenceEquals(Focused(window), RowFrame(window, address)), $"focus did not reach {address}");
        }

        Frame(window, "u8-keys-selected", "");
    });

    [Fact]
    public Task A_refresh_keeps_the_selection_and_the_focus_on_it() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows();
        var address = Served[3].Address;
        Assert.True(shell.Workflows.Select(address));
        Assert.True(RowFrame(window, address).Focus(NavigationMethod.Tab));
        var reads = shell.Workflows.Drawn;

        Server.Push(A.Event(90, "workflow.started"));
        await Until(window, () => shell.Workflows.Drawn > reads, "the Workflows list was not read again");
        Settle(window);

        Assert.Equal(address, shell.Workflows.SelectedAddress);
        Assert.Same(RowFrame(window, address), Focused(window));
    });

    [Fact]
    public Task A_selected_row_that_goes_never_leaves_focus_nowhere() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows();
        var flat = shell.Workflows.Groups.SelectMany(g => g.Rows).Select(r => r.Address).ToList();
        var gone = flat[3];
        Assert.True(shell.Workflows.Select(gone));
        Assert.True(RowFrame(window, gone).Focus(NavigationMethod.Tab));
        var body = GoldenFiles.Node("workflow-instances", "open");
        body["instances"] = new JsonArray([.. body["instances"]!.AsArray().Where(r => (string)r!["address"]! != gone).Select(r => r!.DeepClone())]);
        Server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());

        await shell.Workflows.Refresh(_api, Token);
        var next = flat[4];
        await Reach(window, shell, () => FocusIsOn(window, next));

        Assert.Equal(next, shell.Workflows.SelectedAddress);
    });

    [Fact]
    public Task A_refresh_that_reorders_the_groups_keeps_focus_on_the_same_address() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows();
        var address = shell.Workflows.Groups.SelectMany(g => g.Rows).ElementAt(3).Address;
        Assert.True(shell.Workflows.Select(address));
        Assert.True(RowFrame(window, address).Focus(NavigationMethod.Tab));
        var body = GoldenFiles.Node("workflow-instances", "open");
        body["instances"] = new JsonArray([.. body["instances"]!.AsArray().Reverse().Select(r => r!.DeepClone())]);
        Server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());

        await shell.Workflows.Refresh(_api, Token);
        await Reach(window, shell, () => FocusIsOn(window, address));

        Assert.Equal(address, shell.Workflows.SelectedAddress);
    });

    [Fact]
    public Task A_on_a_desk_task_puts_focus_on_the_pending_outputs_copy() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows();
        var pending = FixtureInstances.Details.First(d => d.Detail.Id == Desk.Id).Detail.Facts.First(f => f.Status == FactStatus.Pending && f.Pending is not null).Key;
        Assert.True(shell.Workflows.Select(Desk.Address));
        Assert.True(RowFrame(window, Desk.Address).Focus(NavigationMethod.Tab));

        Press(window, Key.A);
        await Reach(window, shell, () => Focused(window) is Button { DataContext: FactRowViewModel row } copy && copy.Name == "FactCopyCli" && string.Equals(row.Key, pending, StringComparison.Ordinal));

        Frame(window, "u8-keys-pending-output", "");
    });

    [Fact]
    public Task X_puts_focus_on_Keep_and_Esc_returns_it_to_the_row() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows();
        Assert.True(shell.Workflows.Select(Plain.Address));
        Assert.True(RowFrame(window, Plain.Address).Focus(NavigationMethod.Tab));

        Press(window, Key.X);
        await Reach(window, shell, () => Focused(window) is Button keep && keep.Name == "ExpandedKeep");
        Frame(window, "u8-keys-cancel-ask", "");

        Press(window, Key.Escape);   // the first Esc answers the question (Keep); the second closes the frame
        Assert.True(shell.IsSheetOpen);
        Press(window, Key.Escape);
        await Until(window, () => !shell.IsSheetOpen && Focused(window) is Button { DataContext: InstanceRowViewModel row } open && open.Name == "WorkflowRowOpen" && row.Address == Plain.Address, "focus did not return to the row");
    });

    [Fact]
    public Task A_on_a_protected_answer_shows_the_lock_popover_on_the_row() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows();
        Assert.True(shell.Workflows.Select(Locked.Address));
        Assert.True(RowFrame(window, Locked.Address).Focus(NavigationMethod.Tab));

        Press(window, Key.A);
        await Reach(window, shell, () => shell.Workflows.SelectedRow?.Protected is { IsOpen: true } && window.GetVisualDescendants().OfType<ProtectedActionButton>().Any(b => b.Name == "WorkflowProtected" && b.IsEffectivelyVisible));

        Assert.False(shell.IsSheetOpen);
        Frame(window, "u8-keys-lock-popover", "");
    });

    [Fact]
    public Task B_and_N_say_their_phase_on_the_page() => OnUi(async () =>
    {
        var (window, shell) = await OnWorkflows();
        Assert.True(shell.Workflows.Select(Plain.Address));
        Assert.True(RowFrame(window, Plain.Address).Focus(NavigationMethod.Tab));
        Assert.False(Visible(window, "WorkflowsKeyNote"));

        Press(window, Key.B);
        Settle(window);
        Assert.Equal((true, Labels.HandBackArrives), (Visible(window, "WorkflowsKeyNote"), Named<TextBlock>(window, "WorkflowsKeyNote").Text));

        Assert.Same(RowFrame(window, Plain.Address), Focused(window));
        Frame(window, "u8-keys-phase-note", "");
    });
}
