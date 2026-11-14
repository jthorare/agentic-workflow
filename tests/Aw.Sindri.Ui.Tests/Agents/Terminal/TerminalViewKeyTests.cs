using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Terminal;
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

    // The pane has focus (F6 moved it there); a clipboard of `clipboard` is what a paste reads.
    private static (Window Window, TerminalView View, List<string> Sent, List<string> Pasted) Focused(string? clipboard)
    {
        var f = new TerminalFixture().Attached(TerminalFixture.Lane());
        var sent = new List<string>();
        var pasted = new List<string>();
        var view = new TerminalView { DataContext = f.View, ReadClipboard = () => Task.FromResult(clipboard) };
        view.PaneKeyPressed += (_, e) => sent.Add(e.Name);
        view.PanePasted += (_, e) => pasted.Add(e.Text);
        var window = new Window { Content = view, Width = 1440, Height = 900 };
        window.Show();
        _ = view.Focus();
        Settle(window);
        window.KeyPress(Key.F6, RawInputModifiers.None, PhysicalKey.F6, keySymbol: null);
        Settle(window);
        return (window, view, sent, pasted);
    }

    private static TerminalGrid GridWithHistory(int history) =>
        new(width: 10, height: 5, history, Enumerable.Repeat(TerminalCell.Blank, (history + 5) * 10).ToArray());

    [Fact]
    public async Task Ctrl_Shift_V_and_the_platform_paste_send_the_clipboard_text_as_one_paste_and_never_as_keys()
    {
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TerminalViewKeyTests).Assembly).Dispatch(() =>
        {
            var (window, _, sent, pasted) = Focused("echo hi\nls -l");
            try
            {
                window.KeyPress(Key.V, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.V, keySymbol: null);
                window.KeyPress(Key.V, RawInputModifiers.Control, PhysicalKey.V, keySymbol: null);

                Assert.Equal(["echo hi\nls -l", "echo hi\nls -l"], pasted);
                Assert.Empty(sent);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }

            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task An_empty_clipboard_pastes_nothing(string? clipboard)
    {
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TerminalViewKeyTests).Assembly).Dispatch(() =>
        {
            var (window, _, sent, pasted) = Focused(clipboard);
            try
            {
                window.KeyPress(Key.V, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.V, keySymbol: null);

                Assert.Equal((0, 0), (pasted.Count, sent.Count));
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }

            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Shift_PageUp_and_Shift_PageDown_scroll_the_panes_own_scrollback_and_send_nothing()
    {
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TerminalViewKeyTests).Assembly).Dispatch(() =>
        {
            var (window, view, sent, _) = Focused(clipboard: null);
            try
            {
                view.PaneDrawing.Draw(GridWithHistory(12));
                Settle(window);
                var offsets = new List<int>();

                window.KeyPress(Key.PageUp, RawInputModifiers.Shift, PhysicalKey.PageUp, keySymbol: null);
                offsets.Add(view.PaneScrollOffset);
                window.KeyPress(Key.PageUp, RawInputModifiers.Shift, PhysicalKey.PageUp, keySymbol: null);
                offsets.Add(view.PaneScrollOffset);
                window.KeyPress(Key.PageUp, RawInputModifiers.Shift, PhysicalKey.PageUp, keySymbol: null);
                offsets.Add(view.PaneScrollOffset);
                window.KeyPress(Key.PageDown, RawInputModifiers.Shift, PhysicalKey.PageDown, keySymbol: null);
                offsets.Add(view.PaneScrollOffset);

                Assert.Equal([5, 10, 12, 7], offsets);
                Assert.Empty(sent);
            }
            finally
            {
                window.Close();
                Dispatcher.UIThread.RunJobs();
            }

            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_typed_key_snaps_the_pane_back_to_the_bottom_and_still_goes_to_the_agent()
    {
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TerminalViewKeyTests).Assembly).Dispatch(() =>
        {
            var (window, view, sent, _) = Focused(clipboard: null);
            try
            {
                view.PaneDrawing.Draw(GridWithHistory(12));
                Settle(window);
                window.KeyPress(Key.PageUp, RawInputModifiers.Shift, PhysicalKey.PageUp, keySymbol: null);
                var scrolled = view.PaneScrollOffset;

                window.KeyPress(Key.A, RawInputModifiers.None, PhysicalKey.A, keySymbol: "a");

                Assert.Equal((5, 0), (scrolled, view.PaneScrollOffset));
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
