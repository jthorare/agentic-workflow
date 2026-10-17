using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.Launch;

[Lane(TestLane.Unit)]
public sealed class FakeTerminalViewerContractTests : TerminalViewerContract
{
    protected override ITerminalViewer Create() => new FakeTerminalViewer("warp");

    protected override ViewerRequest Request() =>
        new(new HostRef(HostKind.Tmux, "snd-one"), ["tmux", "attach"], "sindri ITM-1", TimeSpan.FromSeconds(3));
}
