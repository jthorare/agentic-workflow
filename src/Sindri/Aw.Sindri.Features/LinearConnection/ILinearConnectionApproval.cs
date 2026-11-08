using Aw.Kernel;

namespace Aw.Sindri.Features.LinearConnection;

/// <summary>Approves the live profile after a connection change, when exactly the keys that click wrote are the only change waiting.</summary>
internal interface ILinearConnectionApproval
{
    /// <summary>Settles the approval.</summary>
    /// <param name="change">The keys this click wrote; nothing else that waits is approved with them.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>True when the live profile is approved now; false when other changes wait (the owner reviews those in Settings); the error when an approval can't be read or written.</returns>
    public ValueTask<Result<bool>> Settle(LinearConnectionChange change, CancellationToken cancellationToken);
}
