using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Launch;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.Launch.Viewers;

[Lane(TestLane.Unit)]
public sealed class PrintViewerTests : TerminalViewerContract
{
    protected override ITerminalViewer Create() => new PrintViewer();

    protected override ViewerRequest Request() =>
        new(new HostRef(HostKind.Tmux, "snd-one"), ["tmux", "attach"], "sindri ITM-1", TimeSpan.FromSeconds(3));

    [Fact(Timeout = 30_000)]
    public async Task Nothing_opens_and_the_outcome_is_skipped()
    {
        var outcome = await Create().Open(Request(), TestContext.Current.CancellationToken);

        Expect.Equal(ViewerOutcome.Skipped, outcome);
    }

    [Fact(Timeout = 30_000)]
    public async Task Print_is_always_present()
    {
        var facts = await new PrintViewer("detected").Probe(TestContext.Current.CancellationToken);

        Expect.Equal(new TerminalFacts("print", "print", Present: true, "detected", ConfigPath: null, Problem: null, Detail: null), facts);
    }
}
