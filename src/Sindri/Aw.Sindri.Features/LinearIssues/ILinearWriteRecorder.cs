using Aw.Kernel;

namespace Aw.Sindri.Features.LinearIssues;

/// <summary>Records the Linear writes Sindri sent, one ledger event per write, under the current caller's actor (the owner, or 'session:launch:&lt;id&gt;' for a launched session).</summary>
internal interface ILinearWriteRecorder
{
    /// <summary>Appends the event of a write Linear applied.</summary>
    /// <param name="write">The write.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>SND-API-001 when the control plane doesn't hold the lease, or the ledger's error; null when it is recorded.</returns>
    public ValueTask<Error?> Record(LinearWrite write, CancellationToken cancellationToken);
}
