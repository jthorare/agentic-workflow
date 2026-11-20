using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.ViewModels.Agents;
using Aw.Sindri.Ui.Views.Agents;

namespace Aw.Sindri.Ui.Tests.Agents.Terminal;

/// <summary>The chord fact (agents spec §15.3, U7a): F6 and Ctrl+Shift+L reach Sindri's handler before the pane and are never sent to the agent. Cmd+Shift+L is macOS only and is not tested here.</summary>
[Lane(TestLane.Integration)]
public sealed class TerminalViewKeyTests
{
    private static void Settle(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public async Task F6_and_Ctrl_Shift_L_move_focus_and_never_reach_the_agent_while_other_keys_do()
    {
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TerminalViewKeyTests).Assembly).Dispatch(() =>
        {
            var f = new TerminalFixture().Attached(TerminalFixture.Lane());
            var sent = new List<string>();
            var view = new TerminalView { DataContext = f.View };
            view.PaneKeyPressed += (_, e) => sent.Add(e.Name);
            var window = new Window { Content = view, Width = 1440, Height = 900 };
            try
            {
                window.Show();
                _ = view.Focus();
                Settle(window);

                window.KeyPress(Key.F6, RawInputModifiers.None, PhysicalKey.F6, keySymbol: null);
                Settle(window);
                var afterF6 = f.View.Side;
                window.KeyPress(Key.A, RawInputModifiers.None, PhysicalKey.A, keySymbol: "a");
                Settle(window);
                window.KeyPress(Key.L, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.L, keySymbol: null);
                Settle(window);

                Assert.Equal(TerminalSide.Pane, afterF6);
                Assert.Equal(TerminalSide.List, f.View.Side);
                Assert.Equal(["A"], sent);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }

            return true;
        }, TestContext.Current.CancellationToken);
    }
}
