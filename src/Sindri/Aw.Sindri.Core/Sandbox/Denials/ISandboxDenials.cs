using Aw.Kernel;
using Aw.Sindri.Core.Events;

namespace Aw.Sindri.Core.Sandbox.Denials;

/// <summary>Where a run's denial counter sends its 'sandbox.denied' events (sandbox spec §8): the ledger, once per key, so a repeat is a no-op.</summary>
public interface ISandboxDenials
{
    /// <summary>Appends one 'sandbox.denied' event.</summary>
    /// <param name="denied">The event: the 1st, 10th or 100th denial of a target, or the first overflow of a kind.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>Success (also for a repeat), or why the ledger refused.</returns>
    public ValueTask<Result<Unit>> Append(SandboxDenied denied, CancellationToken cancellationToken);
}
