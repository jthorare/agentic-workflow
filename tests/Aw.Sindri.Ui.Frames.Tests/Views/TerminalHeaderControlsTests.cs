using Avalonia.Automation;
using Avalonia.Controls;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;
using Aw.Sindri.Ui.Views.Agents;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Frames.Tests.Views;

/// <summary>The pane header's Stop and Interrupt (#3023, #3334): present with their automation ids for a session Sindri started, the Stop id changes while it asks, and neither shows for any other session.</summary>
[Lane(TestLane.Integration)]
public sealed class TerminalHeaderControlsTests : FrameHost
{
    private static TerminalViewViewModel Attached(bool startedBySindri, FakeTimeProvider clock)
    {
        var view = new TerminalViewViewModel(clock, isMac: false, new FakeSessionControl());
        view.Open(new TerminalTarget { Key = "session:claude-code/ideas", Name = "ideas", Kind = TerminalKind.Listed, SocketKind = "sindri", StartedBySindri = startedBySindri }, TerminalOpenIntent.Look);
        view.SocketOpening();
        _ = view.Ready();
        view.FrameReceived(["a screen"], dead: false);
        return view;
    }

    [Fact]
    public Task Stop_and_Interrupt_carry_their_automation_ids_and_Stop_changes_id_while_it_asks() => OnUi(async () =>
    {
        var vm = Attached(startedBySindri: true, new FakeTimeProvider(A.Noon));
        var window = new Window { Content = new TerminalView { DataContext = vm }, Width = 1440, Height = 900 };
        window.Show();
        Settle(window);

        var stop = Named<Button>(window, "StopButton");
        var interrupt = Named<Button>(window, "InterruptButton");
        var before = (AutomationProperties.GetAutomationId(stop), stop.Content, stop.IsEffectivelyVisible, AutomationProperties.GetAutomationId(interrupt), interrupt.IsEffectivelyVisible);
        _ = await vm.PressStop(Token);
        Settle(window);
        var asking = (AutomationProperties.GetAutomationId(stop), stop.Content);

        Assert.Equal(("TerminalStop", (object?)"Stop…", true, "TerminalInterrupt", true), before);
        Assert.Equal(("TerminalStopConfirm", (object?)"Stop? Press again"), asking);
        window.Close();
    });

    [Fact]
    public Task A_session_Sindri_did_not_start_shows_neither_button() => OnUi(() =>
    {
        var vm = Attached(startedBySindri: false, new FakeTimeProvider(A.Noon));
        var window = new Window { Content = new TerminalView { DataContext = vm }, Width = 1440, Height = 900 };
        window.Show();
        Settle(window);

        Assert.False(Named<Button>(window, "StopButton").IsEffectivelyVisible);
        Assert.False(Named<Button>(window, "InterruptButton").IsEffectivelyVisible);
        window.Close();
        return Task.CompletedTask;
    });
}
