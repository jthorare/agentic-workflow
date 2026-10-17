using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The 'print' viewer (API P10a, unit U6): nothing opens; the attach command travels on 'launch.started' and the app or 'sindri open' prints it.</summary>
/// <param name="source">'file' when launcher.json names it, 'detected' when the launcher fell back to it.</param>
internal sealed class PrintViewer(string source = "file") : ITerminalViewer
{
    /// <inheritdoc/>
    public string Kind => "print";

    /// <inheritdoc/>
    public ValueTask<ViewerOutcome> Open(ViewerRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ValueTask.FromResult(ViewerOutcome.Skipped);
    }

    /// <inheritdoc/>
    public ValueTask<TerminalFacts> Probe(CancellationToken cancellationToken) =>
        ValueTask.FromResult(new TerminalFacts(Kind, Kind, Present: true, source, ConfigPath: null, Problem: null, Detail: null));
}
