using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>Once, at start: removes the old &lt;home&gt;/approved folder and appends one ledger note (#2258 PR 1). A start that finds no folder does nothing.</summary>
/// <param name="settings">Where the folder is.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="ledger">Where the note goes.</param>
/// <param name="events">Builds the note.</param>
internal sealed class ApprovalsDirectoryCleanup(SindriSettings settings, HeldLease held, ILedgerStore ledger, EventFactory events) : IOldApprovalsCleanup
{
    internal SindriSettings Settings { get; } = settings;

    internal HeldLease Held { get; } = held;

    internal ILedgerStore Ledger { get; } = ledger;

    internal EventFactory Events { get; } = events;

    /// <inheritdoc/>
    /// <remarks>The note goes first: a crash between the two repeats the removal, never loses the note.</remarks>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or why the note or the removal failed.</returns>
    public async ValueTask<Result<Unit>> Run(CancellationToken cancellationToken)
    {
        var directory = Settings.ApprovalsDirectory;
        if (!Directory.Exists(directory))
        {
            return Unit.Value;
        }

        if (Held.Current is not { } grant)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        // The event type doubles as the idempotency key, so a removal that keeps failing records the note once, not on every start.
        var appended = await Ledger.Append(Events.Entry(EventTypes.ProfileApprovalsRemoved, new ProfileApprovalsRemoved(directory), EventTypes.ProfileApprovalsRemoved), grant, cancellationToken).ConfigureAwait(false);
        if (appended.IsError)
        {
            return appended.Error;
        }

        try
        {
            Directory.Delete(directory, recursive: true);
            return Unit.Value;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.ServiceFailed.With($"couldn't remove the old approvals folder {directory}: {e.Message} (fix: delete it by hand)");
        }
    }
}
