using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Tests.Launch;

/// <summary>A viewer whose Open never returns until it is cancelled (the launcher bounds it).</summary>
internal sealed class HangingViewer : ITerminalViewer
{
    public string Kind => "hang";

    public async ValueTask<ViewerOutcome> Open(ViewerRequest request, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        return ViewerOutcome.Opened;
    }

    public ValueTask<TerminalFacts> Probe(CancellationToken cancellationToken) =>
        ValueTask.FromResult(new TerminalFacts(Kind, Kind, Present: true, "file", ConfigPath: null, Problem: null, Detail: null));
}
