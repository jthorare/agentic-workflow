using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels.Controls;
using Aw.Sindri.Ui.Views.Controls;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Views;

/// <summary>
/// A catalog control is invokable through its own automation peer (ux-A-3): the UI driver and screen readers act on the
/// CommandControl itself, not only on the button inside it.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class CommandControlPeerTests
{
    private static Task<T> OnUi<T>(Func<T> body) =>
        HeadlessUnitTestSession.GetOrStartForAssembly(typeof(CommandControlPeerTests).Assembly).Dispatch(body, TestContext.Current.CancellationToken);

    private static CommandRowView Row(bool destructive) =>
        new("CloseItem:done", "desk", ["human"], Terminal: false, "POST", "/sindri/items/{id}/close", "sindri item close <item>", "ready", destructive, ReadsViews: false, Shortcut: null);

    private static (Window Window, CommandControl Control, ControlViewModel Vm, List<int> Sent) Show(bool destructive)
    {
        var sent = new List<int>();
        var vm = new ControlViewModel(
            Row(destructive),
            _ =>
            {
                sent.Add(1);
                return Task.FromResult<Result<CommandAcceptedView>>(new CommandAcceptedView("cmd-1", 1));
            },
            new FakeClipboard(),
            new FakeTimeProvider(A.Noon));
        var control = new CommandControl { DataContext = vm };
        var window = new Window { Content = control, Width = 400, Height = 200 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, control, vm, sent);
    }

    [Fact]
    public async Task The_control_has_an_invoke_peer_that_presses_it()
    {
        var (hasInvoke, sent) = await OnUi(() =>
        {
            var (window, control, _, sent) = Show(destructive: false);
            var invoke = ControlAutomationPeer.CreatePeerForElement(control).GetProvider<IInvokeProvider>();
            invoke?.Invoke();
            Dispatcher.UIThread.RunJobs();
            window.Close();
            return (invoke is not null, sent.Count);
        });

        Assert.True(hasInvoke);
        Assert.Equal(1, sent);
    }

    [Fact]
    public async Task Invoking_a_destructive_control_asks_first_and_sends_nothing()
    {
        var (confirming, sent) = await OnUi(() =>
        {
            var (window, control, vm, sent) = Show(destructive: true);
            ControlAutomationPeer.CreatePeerForElement(control).GetProvider<IInvokeProvider>()?.Invoke();
            Dispatcher.UIThread.RunJobs();
            var result = (vm.IsConfirming, sent.Count);
            window.Close();
            return result;
        });

        Assert.True(confirming);
        Assert.Equal(0, sent);
    }
}
