using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Todos;

/// <summary>What the todo writes share (todo spec §4.1, §4.4): the caller and the lease they need, the 'by' and 'via' every event records, and the append that turns losing a version key into SND-TODO-409.</summary>
public static class TodoWrite
{
    /// <summary>The 'via' a request that came with fence=1 records.</summary>
    public const string Pipe = "pipe";

    /// <summary>The caller and the lease grant, or why there is none. Any caller class is admitted (todo spec D2).</summary>
    /// <param name="callers">The caller.</param>
    /// <param name="held">The lease this process holds.</param>
    /// <param name="action">The write, for the message.</param>
    /// <returns>Both, SND-AUTH-002 for no caller, or not running for no lease.</returns>
    public static Result<(CallerContext Caller, LeaseGrant Grant)> Begin(ICallerContextAccessor callers, HeldLease held, string action) =>
        callers.Current is not { } caller ? SindriErrors.AuthRefused.With(action + " needs a caller")
        : held.Current is not { } grant ? SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)")
        : (caller, grant);

    /// <summary>Who an event says wrote it: the caller's actor, with its launch id when it has one ('human:cli', 'session:cli:LCH-…').</summary>
    /// <param name="caller">The caller.</param>
    /// <returns>The 'by'.</returns>
    public static string By(CallerContext caller)
    {
        ArgumentNullException.ThrowIfNull(caller);
        return caller.Launch is { } launch ? caller.Actor + ":" + launch.Value : caller.Actor;
    }

    /// <summary>The 'via' of a request: 'pipe' when it came with fence=1, else null.</summary>
    /// <param name="piped">Whether the CLI had no terminal on stdout.</param>
    /// <returns>The 'via'.</returns>
    public static string? Via(bool piped) => piped ? Pipe : null;

    /// <summary>
    /// The one idempotency key of every state change that moves <paramref name="id"/> to <paramref name="version"/>: 'todo:&lt;id&gt;:v&lt;n&gt;'. The event type is not in it, so two writers
    /// that read the same version (a done and a remove, a change and a mark) share the key and only the first appends (todo spec D9).
    /// </summary>
    /// <param name="id">The todo.</param>
    /// <param name="version">The version the change moves it to.</param>
    /// <returns>The key.</returns>
    public static string VersionKey(TodoId id, long version) => string.Create(CultureInfo.InvariantCulture, $"todo:{id.Value}:v{version}");

    /// <summary>Appends <paramref name="entry"/>.</summary>
    /// <param name="ledger">The ledger.</param>
    /// <param name="grant">The lease grant.</param>
    /// <param name="entry">The entry.</param>
    /// <param name="command">The command it answers.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The command and its sequence, or SND-TODO-409 when another command already wrote the entry's version key.</returns>
    public static async ValueTask<Result<CommandAccepted>> Append(ILedgerStore ledger, LeaseGrant grant, LedgerEntry entry, CommandId command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(entry);
        var appended = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error
            : appended.Value.AlreadyRecorded ? SindriErrors.TodoConflict.With(TodoDecisions.StaleText)
            : new CommandAccepted(command, appended.Value.Sequence);
    }
}
