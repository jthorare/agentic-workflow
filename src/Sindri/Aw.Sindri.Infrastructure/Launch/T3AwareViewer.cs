using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The launcher's viewer: a session the T3 host started opens in the T3 app (its attach command is the 't3code://' link, run without a terminal);
/// every other session goes to the configured terminal viewer. The T3 host is asked by name, so an attach to an earlier launch, which only knows the
/// session name, opens where that session lives.
/// </summary>
/// <param name="terminal">The configured terminal viewer.</param>
/// <param name="t3">The T3 host.</param>
internal sealed class T3AwareViewer(ITerminalViewer terminal, T3SessionHost t3) : ITerminalViewer
{
    /// <inheritdoc/>
    public string Kind => terminal.Kind;

    /// <inheritdoc/>
    public async ValueTask<ViewerOutcome> Open(ViewerRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Host.Kind != HostKind.T3 && !t3.Owns(request.Host.Name))
        {
            return await terminal.Open(request, cancellationToken).ConfigureAwait(false);
        }

        if (request.AttachArgv.Count == 0)
        {
            return ViewerProcess.Failed("the T3 thread has no link to open");
        }

        var opener = request.AttachArgv[0];
        return await ViewerProcess.Output(opener, [.. request.AttachArgv.Skip(1)], cancellationToken).ConfigureAwait(false) is null
            ? ViewerProcess.Failed($"{opener} couldn't open the T3 thread (is T3 Code installed?)")
            : ViewerOutcome.Opened;
    }

    /// <inheritdoc/>
    public ValueTask<TerminalFacts> Probe(CancellationToken cancellationToken) => terminal.Probe(cancellationToken);
}
