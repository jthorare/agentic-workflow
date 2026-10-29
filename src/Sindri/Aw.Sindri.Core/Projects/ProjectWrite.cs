using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Projects;

/// <summary>What the four project writes share (projects page spec §5.3): the caller and the lease they need, and the append that turns losing a version key into SND-PROJECT-409.</summary>
public static class ProjectWrite
{
    /// <summary>The caller and the lease grant, or why there is none.</summary>
    /// <param name="callers">The caller.</param>
    /// <param name="held">The lease this process holds.</param>
    /// <param name="action">The write, for the message.</param>
    /// <returns>Both, SND-AUTH-002 for no caller or an agent session (the desk class lets a session through, but the Projects page is the owner's own), or not running for no lease.</returns>
    public static Result<(CallerContext Caller, LeaseGrant Grant)> Begin(ICallerContextAccessor callers, HeldLease held, string action) =>
        callers.Current is not { } caller ? SindriErrors.AuthRefused.With(action + " needs a caller")
        : caller.Class == CallerClass.Session ? SindriErrors.AuthRefused.With(action + " is the owner's: an agent session can't change the Projects page (fix: the human runs the sindri project command)")
        : held.Current is not { } grant ? SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)")
        : (caller, grant);

    /// <summary>Appends <paramref name="entry"/>.</summary>
    /// <param name="ledger">The ledger.</param>
    /// <param name="grant">The lease grant.</param>
    /// <param name="entry">The entry.</param>
    /// <param name="command">The command it answers.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The command and its sequence, or SND-PROJECT-409 when another command already wrote the entry's version key (a retried call has a new command id, so this is never a replay).</returns>
    public static async ValueTask<Result<CommandAccepted>> Append(ILedgerStore ledger, LeaseGrant grant, LedgerEntry entry, CommandId command, CancellationToken cancellationToken)
    {
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error
            : appended.Value.AlreadyRecorded ? SindriErrors.ProjectConflict.With("This project changed since you read it. Read it again and retry.")
            : new CommandAccepted(command, appended.Value.Sequence);
    }
}
