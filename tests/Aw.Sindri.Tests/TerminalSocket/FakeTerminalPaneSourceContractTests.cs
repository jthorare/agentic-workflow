using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.TerminalSocket;

[Lane(TestLane.Unit)]
public sealed class FakeTerminalPaneSourceContractTests : TerminalPaneSourceContract
{
    protected override ValueTask<ITerminalPaneSource> Create()
    {
        var fake = FakeTerminalPaneSource.FromFixture();
        fake.EchoText = true;   // typed text shows on the screen, like the tmux pane's tty echo
        return ValueTask.FromResult<ITerminalPaneSource>(fake);
    }

    protected override ValueTask<ITerminalPaneSource> CreateWithHistory(int lines)
    {
        var fake = FakeTerminalPaneSource.FromFixture();
        fake.EchoText = true;
        fake.History = [.. Enumerable.Range(1, lines).Select(n => $"history {n.ToString(CultureInfo.InvariantCulture)}")];
        return ValueTask.FromResult<ITerminalPaneSource>(fake);
    }

    protected override ValueTask GrowHistory(ITerminalPaneSource source, int lines)
    {
        ((FakeTerminalPaneSource)source).AppendHistory(lines);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask EnterAlternateScreen(ITerminalPaneSource source)
    {
        ((FakeTerminalPaneSource)source).EnterAlternateScreen();
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<ITerminalPaneSource> CreateOnAlternateScreen()
    {
        var screen = new TerminalFrame(1, [.. Enumerable.Repeat(string.Empty, 36)], 0, 0, 120, 36, Alt: true, InMode: false, Dead: false);
        var fake = new FakeTerminalPaneSource(new TerminalHost("sindri-launch", "fx", "%0"), [screen]) { History = [.. Enumerable.Range(1, 50).Select(n => $"normal screen {n.ToString(CultureInfo.InvariantCulture)}")] };
        return ValueTask.FromResult<ITerminalPaneSource>(fake);
    }

    protected override ValueTask<ITerminalPaneSource> CreateRefusing(string code)
    {
        var fake = FakeTerminalPaneSource.FromFixture();
        fake.OpenFails = (code == SindriErrors.AgentNoPane.Code ? SindriErrors.AgentNoPane : SindriErrors.AgentPaneGone).With("fake");
        return ValueTask.FromResult<ITerminalPaneSource>(fake);
    }
}
