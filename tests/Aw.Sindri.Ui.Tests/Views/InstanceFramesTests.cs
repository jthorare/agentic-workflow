using System.Net;
using System.Text.Json.Nodes;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>U8 key frames (UI spec §18.2: one per state, integration lane) for the instance frame, against the in-memory API serving the instance goldens. Saved under frames/ as u8-frame-*.png.</summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class InstanceFramesTests : FrameHost
{
    private const string Prefix = "u8-frame-";

    private static InstanceDetail Detail(string scenario) => FixtureInstances.Detail(scenario);

    private async Task<(Window Window, ShellViewModel Shell)> Opened(string scenario, FramePhase phase = FramePhase.Shown, RouteAnswer? served = null, bool disclosures = true)
    {
        var (window, shell) = await Connected(width: 1600, height: 1000);
        if (served is not null)
        {
            Server.WorkflowAnswer = served;   // after connecting: the list behind the sheet is read first
        }

        shell.OpenInstance(Detail(scenario).Address, Detail(scenario).Id);
        await Until(window, () => shell.Instance.Phase == phase);
        Settle(window);
        if (disclosures)
        {
            OpenDisclosures(window);
        }

        return (window, shell);
    }

    private static List<TextBlock> Texts(Window window, string name) =>
        [.. window.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Name == name && t.IsEffectivelyVisible)];

    private static List<Button> Buttons(Window window, string name) =>
        [.. Named<Control>(window, "InstanceFrame").GetVisualDescendants().OfType<Button>().Where(b => b.Name == name && b.IsEffectivelyVisible)];   // the Workflows page behind the sheet has its own lock buttons

    private static void Frame(Window window, string name) => Frame(window, name, Prefix);

    private static bool OnNamed(Window window, string name) => string.Equals((window.FocusManager?.GetFocusedElement() as Button)?.Name, name, StringComparison.Ordinal);

    // Opening a frame (a link, no row behind it) puts focus on its first read action (review run 1, V4; UI spec §11.2).
    private static Task FocusedOn(Window window, string name) => Until(window, () => OnNamed(window, name), $"focus is on {(window.FocusManager?.GetFocusedElement() as Control)?.Name}");

    private static void Click(Button button) => button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    [Fact]
    public Task u8_frame_project_build() => OnUi(async () =>
    {
        var (window, shell) = await Opened("project-build", disclosures: false);

        var detail = Detail("project-build");
        Assert.False(Visible(window, "ExpandedBudget"));   // stage details are closed until asked for
        OpenDisclosures(window);
        Assert.Equal(detail.States.Count, Texts(window, "StateNodeText").Count);
        Assert.Equal(detail.Current.Status.Next, Named<TextBlock>(window, "ExpandedNext").Text);
        Assert.Equal(shell.Instance.Identity, Named<TextBlock>(window, "InstanceIdentity").Text);
        Assert.Equal(shell.Instance.Subject!.OpenInText, Named<Button>(window, "InstanceOpenIn").Content?.ToString());
        Assert.True(Visible(window, "ExpandedBudget"));

        var texts = Texts(window, "FrameTabText").Select(t => t.Text).ToList();
        var phases = Texts(window, "FrameTabPhase").Select(t => t.Text).ToList();
        Expect.Sequence(shell.Instance.Tabs!.Tabs.Select(t => t.Text), texts);
        Expect.Sequence(shell.Instance.Tabs.Tabs.Select(t => t.Phase), phases);
        Frame(window, "project-build");   // the frame as the golden serves it, before the pointer is injected

        // a child pointer arrives on a re-read: the button draws its words, and its name leads with them (r3-3)
        Server.WorkflowAnswer = PointerTo(Detail("item-produce"));
        await shell.Instance.Refresh(new Uri("http://127.0.0.1:7410/"), Token);
        await Until(window, () => Visible(window, "ExpandedChild"));
        var pointer = Named<Button>(window, "ExpandedChild");
        Assert.Equal(Labels.OpenInstanceName(shell.Instance.Expanded!.Child!.Text, Detail("item-produce").Address), Heard.Name(pointer));
        Assert.StartsWith(pointer.Content!.ToString()!, Heard.Name(pointer), StringComparison.Ordinal);
        await FocusedOn(window, "InstanceOpenIn");

        // the child opened from the status line's pointer: its frame takes focus, and Back returns to the pointer (a11y-r2-5)
        var (parent, parentAnswer) = (shell.Instance.Address, Server.WorkflowAnswer);
        Server.WorkflowAnswer = null;   // the child is read from its own golden
        Click(Named<Button>(window, "ExpandedChild"));
        await Until(window, () => shell.Instance.Address == Detail("item-produce").Address && shell.Instance.Phase == FramePhase.Shown);
        await FocusedOn(window, shell.Instance.InitialFocus == FrameFocus.OpenIn ? "InstanceOpenIn" : "ExpandedCopyStatus");
        Server.WorkflowAnswer = parentAnswer;
        Click(Named<Button>(window, "SheetBack"));
        await FocusedOn(window, "ExpandedChild");
        Assert.Equal(parent, shell.Instance.Address);
    });

    // The project-build golden with a child pointer on its status (no instance golden shows one); every read answers it.
    private static RouteAnswer PointerTo(InstanceDetail child)
    {
        var body = GoldenFiles.Node("workflow-instance", "project-build");
        body["current"]!["status"]!["child"] = new JsonObject { ["id"] = child.Id, ["address"] = child.Address, ["state"] = "parked", ["reason"] = null };
        return new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());
    }

    [Fact]
    public Task u8_frame_incident_loop() => OnUi(async () =>
    {
        var (window, shell) = await Opened("incident-loop");
        await FocusedOn(window, "ExpandedCopyStatus");

        Assert.Contains(Texts(window, "StateNodeLink"), t => t.Text == Labels.LoopMark);
        Assert.True(Visible(window, "ExpandedAttachPanel"));
        Assert.DoesNotContain("P10a", Named<Panel>(window, "PendingChip").GetVisualDescendants().OfType<TextBlock>().Single().Text, StringComparison.Ordinal);
        Assert.Empty(Buttons(window, "LockButton"));
        Assert.All(Texts(window, "CommandCli"), t => Assert.Equal(TextWrapping.Wrap, t.TextWrapping));   // a long CLI wraps inside the frame, never clipped at the right
        var scrolls = Named<Border>(window, "ExpandedState").GetVisualAncestors().OfType<ScrollViewer>().ToList();
        Assert.Equal("InstanceSheetScroll", Assert.Single(scrolls, sv => sv.Name?.EndsWith("SheetScroll", StringComparison.Ordinal) == true).Name);   // the frame scrolls once
        Assert.Empty(Named<Border>(window, "ExpandedState").GetVisualDescendants().OfType<ScrollViewer>());   // and the expanded state has no scroll region of its own
        Frame(window, "incident-loop");

        // Esc during the cancel confirm answers Keep and returns focus to the ask, with the sheet still open (V5)
        Click(Buttons(window, "CommandAsk").Single());
        await Until(window, () => shell.Instance.Expanded!.IsConfirmingCancel);
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.None, keySymbol: null);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal((false, true), (shell.Instance.Expanded!.IsConfirmingCancel, shell.IsSheetOpen));
        await FocusedOn(window, "CommandAsk");
    });

    [Fact]
    public Task u8_frame_triage_desk() => OnUi(async () =>
    {
        var (window, _) = await Opened("triage-desk");

        var served = Detail("triage-desk").Current.Status.Commands;
        Expect.Sequence(served.Select(c => c.Cli), Texts(window, "CommandCli").Select(t => t.Text));
        Assert.Equal(served.Count(c => c.Verb != StatusVerb.Takeover), Texts(window, "CommandPending").Count);   // Take over is sent by the app: its button stands where the pending chip was
        Assert.Equal(served.Count(c => c.Verb == StatusVerb.Takeover), Buttons(window, "CommandTakeOver").Count);
        var command = window.GetVisualDescendants().OfType<Border>().First(b => b.Name == "CommandLabelName" && b.IsEffectivelyVisible);   // the peer's name, not the view model's (a TextBlock's peer ignores one)
        var heard = Heard.Wrapper(command);
        Assert.Equal(((ExpandedCommandViewModel)command.DataContext!).Name, heard);
        Assert.Contains(Labels.AvailableWithU8b, heard, StringComparison.Ordinal);
        Frame(window, "triage-desk");
    });

    [Fact]
    public Task u8_frame_escalated_gate() => OnUi(async () =>
    {
        var (window, shell) = await Opened("escalated-gate");

        _ = Assert.Single(Buttons(window, "LockButton"));
        Assert.Contains(Texts(window, "StateNodeText"), t => t.Text!.Contains(Labels.EngineBadge(EngineOwnedKind.Escalated, Detail("escalated-gate").EngineOwned!.Reason), StringComparison.Ordinal));
        Assert.Equal((true, "gate"), (shell.Instance.HasDetail, shell.Instance.Detail));
        Frame(window, "escalated-gate");
    });

    [Fact]
    public Task u8_frame_dev_desk_round_trip() => OnUi(async () =>
    {
        var (window, _) = await Opened("dev-desk-round-trip");

        Assert.Equal(Detail("dev-desk-round-trip").Current.Status.Next, Named<TextBlock>(window, "ExpandedNext").Text);
        Assert.True(Visible(window, "ExpandedTimeout"));
        Frame(window, "dev-desk-round-trip");
    });

    [Fact]
    public Task u8_frame_dev_triage_three_state() => OnUi(async () =>
    {
        var (window, _) = await Opened("dev-triage-three-state");

        Assert.Equal(Detail("dev-triage-three-state").States.Count, Texts(window, "StateNodeText").Count);
        Assert.Equal(Detail("dev-triage-three-state").Current.Status.Next, Named<TextBlock>(window, "ExpandedNext").Text);
        Frame(window, "dev-triage-three-state");
    });

    [Fact]
    public Task u8_frame_transition_list() => OnUi(async () =>
    {
        var (window, _) = await Opened("transition-list");

        Assert.Equal(Labels.OrderedByTransitions, Named<TextBlock>(window, "BandOrderNote").Text);
        Assert.Equal(Detail("transition-list").States.Count, Texts(window, "StateNodeText").Count);
        Assert.Equal(Detail("transition-list").States.Count - 1, Named<ItemsControl>(window, "StateNodes").ItemCount);
        Frame(window, "transition-list");
    });

    [Fact]
    public Task u8_frame_done() => OnUi(async () =>
    {
        var (window, shell) = await Opened("done");

        Assert.Equal((true, Labels.InstanceDone), (Visible(window, "InstanceNote"), Named<TextBlock>(window, "InstanceNote").Text));
        Assert.Empty(Texts(window, "CommandCli"));
        Assert.False(Visible(window, "ExpandedNext"));
        Assert.Equal(Detail("done").Current.Status.Header.State, shell.Instance.StateText);
        Frame(window, "done");
    });

    [Fact]
    public Task u8_frame_not_found() => OnUi(async () =>
    {
        var (window, _) = await Opened("project-build", FramePhase.NotFound, RouteAnswer.Problem("workflow-instance", "not-found"));
        await FocusedOn(window, "InstanceBack");

        Assert.Equal((string)GoldenFiles.Node("workflow-instance", "not-found")["detail"]!, Named<TextBlock>(window, "InstanceProblem").Text);
        Assert.Equal((true, false, true), (Visible(window, "InstanceProblem"), Visible(window, "InstanceBand"), Visible(window, "InstanceBack")));
        Frame(window, "not-found");
    });

    [Fact]
    public Task u8_frame_taken_over() => OnUi(async () =>
    {
        var body = (JsonObject)GoldenFiles.Node("workflow-instance", "project-build");
        var status = GoldenFiles.Node("workflow-status", "taken-over");
        body["current"]!["status"] = status.DeepClone();
        body["address"] = status["header"]!["address"]!.DeepClone();
        var cover = GoldenFiles.Node("workflow-instances", "open")["instances"]!.AsArray().First(r => r!["engineOwned"] is not null || r["states"]!.AsArray().Any(s => s!["badge"] is not null))!["states"]!.AsArray().First(s => s!["badge"] is not null)!;
        body["states"]!.AsArray().First(s => (string)s!["id"]! == (string)cover["id"]!)!["badge"] = cover["badge"]!.DeepClone();
        Server.WorkflowAnswer = new RouteAnswer(HttpStatusCode.OK, body.ToJsonString());
        var (window, shell) = await Opened("project-build");

        Assert.True(Visible(window, "ExpandedPaused"));
        Assert.Contains(Labels.Duration(FixtureInstances.Status("taken-over").Paused!.TimeoutLeftSeconds), Named<TextBlock>(window, "ExpandedPaused").Text, StringComparison.Ordinal);
        Expect.Sequence(FixtureInstances.Status("taken-over").Commands.Select(c => c.Cli), Texts(window, "CommandCli").Select(t => t.Text));
        Assert.Equal("taken-over", shell.Instance.StateText);
        Assert.False(Visible(window, "ExpandedAttachPanel"));
        Frame(window, "taken-over");
    });

    [Fact]
    public Task u8_frame_cancel_confirm() => OnUi(async () =>
    {
        var (window, shell) = await Opened("project-build");
        var served = Detail("project-build").Current.Status.Commands.Single(c => c.Verb == StatusVerb.Cancel).Confirm;
        var ask = Buttons(window, "CommandAsk").Single();
        Assert.True(ask.Focus(NavigationMethod.Tab));

        shell.Instance.Expanded!.AskCancel();
        Settle(window);

        Assert.Equal((true, served), (Visible(window, "ExpandedConfirm"), Named<TextBlock>(window, "ExpandedConfirmLine").Text));
        await Until(window, () => string.Equals((window.FocusManager?.GetFocusedElement() as Button)?.Name, "ExpandedKeep", StringComparison.Ordinal), "Keep did not take focus");
        Frame(window, "cancel-confirm");
        shell.Instance.Expanded.Keep();
        await Until(window, () => ReferenceEquals(window.FocusManager?.GetFocusedElement(), Buttons(window, "CommandAsk").Single()), "focus did not return to Cancel");
        Assert.False(Visible(window, "ExpandedConfirm"));
    });

    [Fact]
    public Task u8_frame_lock_popover() => OnUi(async () =>
    {
        var (window, _) = await Opened("escalated-gate");
        var button = Buttons(window, "LockButton").Single();

        button.Flyout!.ShowAt(button);
        Settle(window);

        var root = (Avalonia.Visual)((Flyout)button.Flyout).Content!;
        Assert.True(button.Flyout.IsOpen);
        Assert.Equal(Detail("escalated-gate").Current.Status.Answer!.Cli, Named<TextBlock>(root, "ServedCli").Text);
        Frame(window, "lock-popover");
        button.Flyout.Hide();
    });

    [Fact]
    public Task Focus_order_reads_before_it_acts() => OnUi(async () =>
    {
        var (window, _) = await Opened("project-build");
        Assert.True(Named<Button>(window, "InstanceOpenIn").Focus(NavigationMethod.Tab));

        var walked = new List<string>();
        for (var i = 0; i < 40 && !walked.Contains("CommandAsk"); i++)
        {
            window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.None, keySymbol: null);
            Dispatcher.UIThread.RunJobs();
            walked.Add((window.FocusManager?.GetFocusedElement() as Control)?.Name ?? string.Empty);
        }

        var (copy, ask) = (walked.IndexOf("ExpandedCopyStatus"), walked.IndexOf("CommandAsk"));
        Assert.True(copy >= 0 && ask >= 0, "the walk reached " + string.Join(", ", walked));
        Assert.True(copy < ask);
        Assert.DoesNotContain("ExpandedKeep", walked, StringComparer.Ordinal);
    });
}
