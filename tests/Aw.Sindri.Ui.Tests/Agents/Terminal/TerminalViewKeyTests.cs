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

    [Fact]
    public async Task Ctrl_Shift_V_Shift_Insert_and_Cmd_V_send_the_clipboard_text_as_one_paste_and_never_as_keys()
    {
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TerminalViewKeyTests).Assembly).Dispatch(() =>
        {
            var (window, _, sent, pasted) = Focused("echo hi\nls -l");
            try
            {
                window.KeyPress(Key.V, RawInputModifiers.Control | RawInputModifiers.Shift, PhysicalKey.V, keySymbol: null);
                window.KeyPress(Key.Insert, RawInputModifiers.Shift, PhysicalKey.Insert, keySymbol: null);
                window.KeyPress(Key.V, RawInputModifiers.Meta, PhysicalKey.V, keySymbol: null);

                Assert.Equal(["echo hi\nls -l", "echo hi\nls -l", "echo hi\nls -l"], pasted);
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

    // Claude Code pastes images on Ctrl+V and vim reads it as literal-next: plain Ctrl+V is a key for the agent, never a paste.
    [Fact]
    public async Task Plain_Ctrl_V_goes_to_the_agent_as_a_key_and_pastes_nothing()
    {
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TerminalViewKeyTests).Assembly).Dispatch(() =>
        {
            var (window, _, sent, pasted) = Focused("clipboard text");
            try
            {
                window.KeyPress(Key.V, RawInputModifiers.Control, PhysicalKey.V, keySymbol: null);

                Assert.Equal(["Ctrl+V"], sent);
                Assert.Empty(pasted);
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
    public async Task Shift_PageUp_and_Shift_PageDown_ask_for_a_screen_back_or_forward_and_send_no_key()
    {
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TerminalViewKeyTests).Assembly).Dispatch(() =>
        {
            var (window, view, sent, _) = Focused(clipboard: null);
            var pages = new List<int>();
            view.PaneScrollRequested += (_, e) => pages.Add(e.Pages);
            try
            {
                window.KeyPress(Key.PageUp, RawInputModifiers.Shift, PhysicalKey.PageUp, keySymbol: null);
                window.KeyPress(Key.PageUp, RawInputModifiers.Shift, PhysicalKey.PageUp, keySymbol: null);
                window.KeyPress(Key.PageDown, RawInputModifiers.Shift, PhysicalKey.PageDown, keySymbol: null);

                Assert.Equal([1, 1, -1], pages);
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
    public async Task A_typed_key_after_Shift_PageUp_goes_to_the_agent_and_asks_for_no_scroll()
    {
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TerminalViewKeyTests).Assembly).Dispatch(() =>
        {
            var (window, view, sent, _) = Focused(clipboard: null);
            var pages = new List<int>();
            view.PaneScrollRequested += (_, e) => pages.Add(e.Pages);
            try
            {
                window.KeyPress(Key.PageUp, RawInputModifiers.Shift, PhysicalKey.PageUp, keySymbol: null);

                window.KeyPress(Key.A, RawInputModifiers.None, PhysicalKey.A, keySymbol: "a");

                Assert.Equal([1], pages);
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

    [Fact]
    public async Task The_header_says_how_far_back_the_pane_is_and_the_line_goes_at_live()
    {
        _ = await HeadlessUnitTestSession.GetOrStartForAssembly(typeof(TerminalViewKeyTests).Assembly).Dispatch(() =>
        {
            var f = new TerminalFixture().Attached(TerminalFixture.Lane());
            var view = new TerminalView { DataContext = f.View };
            var window = new Window { Content = view, Width = 1440, Height = 900 };
            try
            {
                window.Show();
                Settle(window);
                var line = view.FindControl<TextBlock>("ScrollBackLine") ?? throw new InvalidOperationException("no ScrollBackLine");
                var atStart = line.IsVisible;

                f.View.ScrolledBack(35);
                Settle(window);
                var (back, text) = (line.IsVisible, line.Text);
                f.View.ScrolledBack(0);
                Settle(window);

                Assert.Equal((false, true, "Scrolled back 35 rows · any key returns to live", false), (atStart, back, text, line.IsVisible));
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
