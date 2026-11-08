using Aw.Kernel;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The call of a 'jev' node. Jev is a third party: the adapter redacts what it sends and caps it at 160,000 characters (Core cannot reference Aw.Redaction).</summary>
public interface IJevPort
{
    /// <summary>Asks Jev.</summary>
    /// <param name="call">The text and questions.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The answers, or the judge's reason code in the message: 'no-api-key', 'network-error', 'http-NNN' or 'timeout'.</returns>
    public ValueTask<Result<JevOutcome>> Ask(JevCall call, CancellationToken cancellationToken);
}
