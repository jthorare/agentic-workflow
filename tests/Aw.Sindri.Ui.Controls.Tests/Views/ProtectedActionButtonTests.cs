using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Components;
using Aw.Sindri.Ui.Views.Components;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Controls.Tests.Views;

/// <summary>The lock button under the headless platform: its label and title, the popover it opens, Esc, and Copy.</summary>
[Lane(TestLane.Integration)]
public sealed class ProtectedActionButtonTests
{
    private const string Subject = "REQ-01JD4";
    private const string Cli = "sindri decision accept REQ-01JD4";

    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(ProtectedActionButtonTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static (Window Window, ProtectedActionButton Control, Button Lock, FakeClipboard Clipboard) Show(string cli = Cli)
    {
        var clipboard = new FakeClipboard();
        var vm = new ProtectedActionViewModel(new ProtectedAction("Accept", cli, Subject), clipboard, new FakeTimeProvider(A.Noon));
        var control = new ProtectedActionButton { DataContext = vm };
        var window = new Window { Content = control, Width = 600, Height = 300 };
        window.Show();
        Settle(window);
        return (window, control, Named<Button>(control, "LockButton"), clipboard);
    }

    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static T Named<T>(Visual root, string name)
        where T : Control => root.GetVisualDescendants().OfType<T>().First(c => c.Name == name);

    private static void Press(Window window, Key key, PhysicalKey physical) => window.KeyPress(key, RawInputModifiers.None, physical, keySymbol: null);

    [Fact]
    public async Task The_button_reads_the_lock_label_has_the_title_and_a_name_with_the_subject()
    {
        var (label, tip, name, help) = await OnUi(() =>
        {
            var (window, _, button, _) = Show();
            var result = (button.Content?.ToString(), ToolTip.GetTip(button)?.ToString(), AutomationProperties.GetName(button), AutomationProperties.GetHelpText(button));
            window.Close();
            return result;
        });

        Assert.Equal("⌂ Accept · terminal", label);
        Assert.Equal("Needs a typed confirmation at a terminal", tip);
        Assert.Equal($"Accept {Subject}", name);
        Assert.Equal("Needs a typed confirmation at a terminal", help);
    }

    [Fact]
    public async Task Click_opens_a_popover_with_the_served_cli_Copy_and_Open_in_an_agent()
    {
        var (open, cli, copy, copyName, agent, agentName, pending) = await OnUi(() =>
        {
            var (window, control, button, _) = Show();
            _ = button.Focus(NavigationMethod.Tab);
            Press(window, Key.Enter, PhysicalKey.Enter);
            Settle(window);
            var root = (Visual)((Flyout)button.Flyout!).Content!;
            _ = control;
            var result = (button.Flyout.IsOpen, Named<TextBlock>(root, "ServedCli").Text, Named<Button>(root, "CopyButton").Content?.ToString(),
                AutomationProperties.GetName(Named<Button>(root, "CopyButton")), Named<Control>(root, "OpenInAgent").IsEffectivelyVisible,
                AutomationProperties.GetName(Named<Control>(root, "OpenInAgent")), Named<Control>(root, "OpenInAgentChip").IsEffectivelyVisible);
            _ = Named<Button>(root, "OpenInAgent");   // a Button, so it is a control a screen reader can reach
            window.Close();
            return result;
        });

        Assert.True(open);
        Assert.Equal(Cli, cli);
        Assert.Equal("Copy", copy);
        Assert.Equal($"Copy: {Cli}", copyName);
        Assert.True(agent);
        Assert.Equal($"Open in an agent: {Subject}, not available in the app yet", agentName);
        Assert.True(pending);
    }

    // a11y-1: a served command holding a bidi or control character is not shown and not copied; the popover says so, and Copy's name says there is no command.
    [Fact]
    public async Task An_unsafe_served_command_is_not_shown_and_Copy_is_named_no_command_served()
    {
        var (shown, shownName, copyName) = await OnUi(() =>
        {
            var (window, _, button, _) = Show("sindri decision accept \u202eREQ-01JD4");
            _ = button.Focus(NavigationMethod.Tab);
            Press(window, Key.Enter, PhysicalKey.Enter);
            Settle(window);
            var root = (Visual)((Flyout)button.Flyout!).Content!;
            var cli = Named<TextBlock>(root, "ServedCli");
            var result = (cli.Text, AutomationProperties.GetName(cli), AutomationProperties.GetName(Named<Button>(root, "CopyButton")));
            window.Close();
            return result;
        });

        Assert.Equal("The served command can't be shown", shown);
        Assert.Equal("The served command can't be shown", shownName);
        Assert.Equal("Copy: no command served", copyName);
    }

    // The popover's focus targets are the controls a screen reader and the Tab key reach (a11y-0): each is a control
    // element with a name, and Open in an agent is a disabled Button, not a focusable panel.
    [Fact]
    public async Task The_popovers_controls_are_exposed_with_names_and_Open_in_an_agent_is_a_disabled_button()
    {
        var rows = await OnUi(() =>
        {
            var (window, _, button, _) = Show();
            _ = button.Focus(NavigationMethod.Tab);
            Press(window, Key.Enter, PhysicalKey.Enter);
            Settle(window);
            var root = (Visual)((Flyout)button.Flyout!).Content!;
            var agent = Named<Button>(root, "OpenInAgent");
            var result = new[] { Named<Button>(root, "CopyButton"), agent }
                .Select(c => (c.Name, Control: ControlAutomationPeer.CreatePeerForElement(c).IsControlElement(), AutomationProperties.GetName(c), c.IsEnabled)).ToList();
            window.Close();
            return result;
        });

        Assert.All(rows, r => Assert.True(r.Control));
        Assert.All(rows, r => Assert.False(string.IsNullOrWhiteSpace(r.Item3)));
        Assert.Equal(("OpenInAgent", false), (rows[1].Name, rows[1].IsEnabled));
    }

    // a11y-0: the Copied region joins the tree when the popover opens (as Attach's does when its menu opens), so a list of
    // lock buttons carries one only for the button in use.
    [Fact]
    public async Task The_Copied_region_joins_the_tree_when_the_popover_opens()
    {
        var (before, after) = await OnUi(() =>
        {
            var (window, control, button, _) = Show();
            var region = Named<TextBlock>(control, "CopyAnnouncementRegion");
            var beforeOpen = region.IsVisible;
            _ = button.Focus(NavigationMethod.Tab);
            Press(window, Key.Enter, PhysicalKey.Enter);
            Settle(window);
            var result = (beforeOpen, region.IsVisible);
            window.Close();
            return result;
        });

        Assert.False(before);
        Assert.True(after);
    }

    [Fact]
    public async Task Copy_puts_the_served_cli_on_the_clipboard()
    {
        var (text, label) = await OnUi(() =>
        {
            var (window, _, button, clipboard) = Show();
            _ = button.Focus(NavigationMethod.Tab);
            Press(window, Key.Enter, PhysicalKey.Enter);
            Settle(window);
            var root = (Visual)((Flyout)button.Flyout!).Content!;
            Named<Button>(root, "CopyButton").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
            Settle(window);
            var result = (clipboard.Text, Named<Button>(root, "CopyButton").Content?.ToString());
            window.Close();
            return result;
        });

        Assert.Equal(Cli, text);
        Assert.Equal("Copied", label);
    }

    [Fact]
    public async Task Esc_closes_the_popover_and_returns_focus()
    {
        var (closed, focused) = await OnUi(() =>
        {
            var (window, _, button, _) = Show();
            _ = button.Focus(NavigationMethod.Tab);
            Press(window, Key.Enter, PhysicalKey.Enter);
            Settle(window);
            Press(window, Key.Escape, PhysicalKey.Escape);
            Settle(window);
            var result = (button.Flyout?.IsOpen == false, button.IsFocused);
            window.Close();
            return result;
        });

        Assert.True(closed);
        Assert.True(focused);
    }

    // W6 (a11y-3): with neither Copy nor Open in an agent enabled, the popover would hold nothing to focus; the served-CLI
    // line takes focus (exposed as Text with a name) and Esc still returns focus to the lock button.
    [Fact]
    public async Task An_unsafe_command_popover_focuses_the_served_line_and_Esc_returns_to_the_lock_button()
    {
        var (onCli, type, name, back) = await OnUi(() =>
        {
            var (window, _, button, _) = Show("sindri decision accept \u202eREQ-01JD4");
            _ = button.Focus(NavigationMethod.Tab);
            Press(window, Key.Enter, PhysicalKey.Enter);
            Settle(window);
            var cli = Named<TextBlock>((Visual)((Flyout)button.Flyout!).Content!, "ServedCli");
            var peer = ControlAutomationPeer.CreatePeerForElement(cli);
            var first = (window.FocusManager?.GetFocusedElement() == cli, peer.GetAutomationControlType(), peer.GetName());
            Press(window, Key.Escape, PhysicalKey.Escape);
            Settle(window);
            var result = (first.Item1, first.Item2, first.Item3, button.IsFocused);
            window.Close();
            return result;
        });

        Assert.True(onCli);
        Assert.Equal(AutomationControlType.Text, type);
        Assert.Equal("The served command can't be shown", name);
        Assert.True(back);
    }

    // testing-3 / arch-1: with Copy enabled the popover has a focusable action, so the served line is not a focus stop and
    // opening does not move focus to it; with none (the test above) it is the stop.
    [Fact]
    public async Task A_safe_command_popover_does_not_focus_the_served_line()
    {
        var (focusable, onCli, agentEnabled) = await OnUi(() =>
        {
            var (window, _, button, _) = Show();
            _ = button.Focus(NavigationMethod.Tab);
            Press(window, Key.Enter, PhysicalKey.Enter);
            Settle(window);
            var root = (Visual)((Flyout)button.Flyout!).Content!;
            var cli = Named<TextBlock>(root, "ServedCli");
            var result = (cli.Focusable, window.FocusManager?.GetFocusedElement() == cli, Named<Button>(root, "OpenInAgent").IsEnabled);
            window.Close();
            return result;
        });

        Assert.False(focusable);
        Assert.False(onCli);
        Assert.False(agentEnabled);   // pending: the control stays disabled until a phase serves the action
    }
}
