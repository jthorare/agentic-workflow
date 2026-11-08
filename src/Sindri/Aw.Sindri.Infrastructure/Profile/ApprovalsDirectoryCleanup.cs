using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>Once, at start: removes the old &lt;home&gt;/approved folder and appends one ledger note (#2258 PR 1). Red stub; the fix lands in the next commit.</summary>
/// <param name="settings">Where the folder is.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="ledger">Where the note goes.</param>
/// <param name="events">Builds the note.</param>
internal sealed class ApprovalsDirectoryCleanup(SindriSettings settings, HeldLease held, ILedgerStore ledger, EventFactory events)
{
    internal SindriSettings Settings { get; } = settings;

    internal HeldLease Held { get; } = held;

    internal ILedgerStore Ledger { get; } = ledger;

    internal EventFactory Events { get; } = events;

    /// <summary>Removes the folder when it is there, and says so in the ledger.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success.</returns>
    public ValueTask<Result<Unit>> Run(CancellationToken cancellationToken) => throw new NotSupportedException();
}
