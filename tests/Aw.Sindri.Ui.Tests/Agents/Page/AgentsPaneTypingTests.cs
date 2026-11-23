using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Terminal;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.Tests.Views;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.Views.Agents;

namespace Aw.Sindri.Ui.Tests.Agents.Page;

/// <summary>
/// Typing into the live pane through the real window (#3729 review): when the pane takes the keys (Enter again on the shown row, or F6 from the list), keyboard focus
/// moves into it, so a typed key goes to the agent and never to the page (N, J) or the shell.
/// </summary>
[Lane(TestLane.Integration)]
[Collection(HeadlessWindows.Name)]
public sealed class AgentsPaneTypingTests : FrameHost
{
    private const string ReadyJson = "{\"t\":\"ready\",\"v\":1,\"host\":{\"socket\":\"s\",\"session\":\"x\",\"pane\":\"%3\"}}";

    // The fixture's one session with a tmux pane (%3): claude-code, lane ideas.
    private const string WithPane = "aaaaaaaa-0000-4000-8000-000000000001";

    private static void Tap(Window window, Key key, PhysicalKey physical, string? symbol = null)
    {
        window.KeyPress(key, RawInputModifiers.None, physical, symbol);
        Dispatcher.UIThread.RunJobs();
    }

    // The Agents page with a live socket that stays open, the row with a pane selected through the window's J, and its pane open through Enter (focus on the list).
    private async Task<(Window Window, ShellViewModel Shell, List<string> Typed)> PaneOpen()
    {
        Terminal = new FakeTerminalConnector
        {
            Script = [ReadyJson, FakeTerminalConnector.FixtureOut().First(m => TerminalSocketMessages.Parse(m).Kind == TerminalServerKind.Frame)],
            StaysOpen = true,
        };
        var (window, shell) = await Connected(width: 1280);
        shell.GoTo(PageId.Agents);
        await Until(window, () => shell.Agents.Drawn > 0, "the Agents page never read the list");
        var typed = new List<string>();
        Named<TerminalView>(window, "TerminalPanel").PaneKeyPressed += (_, e) => typed.Add(e.Name);
        for (var i = 0; i < 6 && shell.Agents.SelectedKey?.Contains(WithPane, StringComparison.Ordinal) != true; i++)
        {
            Tap(window, Key.J, PhysicalKey.J, "j");
        }

        Assert.Contains(WithPane, shell.Agents.SelectedKey ?? string.Empty, StringComparison.Ordinal);
        Tap(window, Key.Enter, PhysicalKey.Enter);
        await Until(window, () => shell.Agents.IsTerminalOpen && shell.Agents.Terminal.State == ViewModels.Agents.TerminalState.Attached, "the pane never attached");
        return (window, shell, typed);
    }

    [Fact]
    public Task Enter_again_on_the_shown_row_sends_typed_keys_to_the_agent_not_the_page() => OnUi(async () =>
    {
        var (window, shell, typed) = await PaneOpen();
        var selected = shell.Agents.SelectedKey;

        Tap(window, Key.Enter, PhysicalKey.Enter);
        Settle(window);
        Tap(window, Key.A, PhysicalKey.A, "a");
        Tap(window, Key.N, PhysicalKey.N, "n");
        Tap(window, Key.J, PhysicalKey.J, "j");

        Assert.Equal(["A", "N", "J"], typed);
        Assert.False(shell.NewAgent.IsOpen);
        Assert.Equal(selected, shell.Agents.SelectedKey);
    });

    [Fact]
    public Task F6_from_the_list_moves_typing_into_the_pane() => OnUi(async () =>
    {
        var (window, shell, typed) = await PaneOpen();

        Tap(window, Key.F6, PhysicalKey.F6);
        Settle(window);
        Tap(window, Key.A, PhysicalKey.A, "a");

        Assert.Equal(ViewModels.Agents.TerminalSide.Pane, shell.Agents.Terminal.Side);
        Assert.Equal(["A"], typed);
        Assert.False(shell.NewAgent.IsOpen);
    });
}
