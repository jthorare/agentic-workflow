using Aw.Redaction;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>The 'default-terminal' viewer (#4518): opens the user's registered default terminal. Stub until the green commit.</summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
internal sealed class DefaultTerminalViewer(DefaultTerminalOptions options, ISessionHost host, TimeProvider time, IRedactor? redactor = null) : ITerminalViewer
{
    /// <inheritdoc/>
    public string Kind => "default-terminal";

    /// <inheritdoc/>
    public ValueTask<ViewerOutcome> Open(ViewerRequest request, CancellationToken cancellationToken)
    {
        _ = (options, host, time, redactor);
        throw new NotSupportedException("default-terminal is not built yet");
    }

    /// <inheritdoc/>
    public ValueTask<TerminalFacts> Probe(CancellationToken cancellationToken) => throw new NotSupportedException("default-terminal is not built yet");
}
