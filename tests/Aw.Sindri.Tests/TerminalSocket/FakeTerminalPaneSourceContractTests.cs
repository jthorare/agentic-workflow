using Aw.Kernel.Testing;
using Aw.Sindri.Core;
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

    protected override ValueTask<ITerminalPaneSource> CreateRefusing(string code)
    {
        var fake = FakeTerminalPaneSource.FromFixture();
        fake.OpenFails = (code == SindriErrors.AgentNoPane.Code ? SindriErrors.AgentNoPane : SindriErrors.AgentPaneGone).With("fake");
        return ValueTask.FromResult<ITerminalPaneSource>(fake);
    }
}
