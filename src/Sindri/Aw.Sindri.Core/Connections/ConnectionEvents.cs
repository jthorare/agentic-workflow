using System.Globalization;
using Aw.Sindri.Core.Connections.Polling;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Core.Connections;

/// <summary>
/// Records 'connection.signed-in' (connections PR 10b) when a connection that needed a new sign-in is connected again, which clears its Needs-you row. It is a
/// courtesy to the list, not part of the sign-in: when this process holds no lease, or the append fails, the sign-in or the test still succeeded, so nothing is returned.
/// It also holds the clock the connection slices stamp their rows with.
/// </summary>
/// <param name="held">The lease the event is fenced by.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="clock">The time.</param>
public sealed class ConnectionEvents(HeldLease held, ILedgerStore ledger, EventFactory events, TimeProvider clock)
{
    private readonly TimeProvider _clock = clock;

    /// <summary>Gets the current time on the port the slices share.</summary>
    public DateTimeOffset Now => _clock.GetUtcNow();

    /// <summary>Appends 'connection.signed-in' for <paramref name="name"/>.</summary>
    /// <param name="name">The connection.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>A task that ends when the event is recorded or skipped.</returns>
    public async ValueTask SignedIn(ConnectionName name, CancellationToken cancellationToken)
    {
        if (held.Current is not { } grant)
        {
            return;
        }

        var at = Now;
        var entry = events.Entry(EventTypes.ConnectionSignedIn, new ConnectionSignedIn(name.ToString(), at), string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ConnectionSignedIn}:{name}:{at.UtcTicks}"));
        _ = await ledger.Append(entry, grant, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Appends 'connection.needs-sign-in' for <paramref name="name"/>, which projects its Needs-you row. Like <see cref="SignedIn"/> it is skipped when no lease is held and ignores a failed append: the status row is already written.</summary>
    /// <param name="name">The connection.</param>
    /// <param name="reason">Why (a provider's error code or fixed text, never a token).</param>
    /// <param name="grant">The lease to append under; null uses the one this process holds.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>A task that ends when the event is recorded or skipped.</returns>
    public async ValueTask NeedsSignIn(ConnectionName name, string reason, LeaseGrant? grant, CancellationToken cancellationToken)
    {
        if ((grant ?? held.Current) is not { } lease)
        {
            return;
        }

        var at = Now;
        var entry = events.Entry(EventTypes.ConnectionNeedsSignIn, new ConnectionNeedsSignIn(name.ToString(), reason, at), string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ConnectionNeedsSignIn}:{name}:{at.UtcTicks}"));
        _ = await ledger.Append(entry, lease, cancellationToken).ConfigureAwait(false);
    }
    /// <summary>Appends 'connection.polled' (counts only) for a poll that returned events. Skipped when no lease is held; a failed append is ignored: the runs already started.</summary>
    /// <param name="counts">The connection, the event kind and the counts.</param>
    /// <param name="grant">The lease to append under; null uses the one this process holds.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>A task that ends when the event is recorded or skipped.</returns>
    public async ValueTask Polled(PollCounts counts, LeaseGrant? grant, CancellationToken cancellationToken)
    {
        if ((grant ?? held.Current) is not { } lease)
        {
            return;
        }

        var at = Now;
        var entry = events.Entry(EventTypes.ConnectionPolled, new ConnectionPolled(counts.Name.ToString(), counts.Kind.Key, counts.Count, counts.Started, counts.Skipped, at), string.Create(CultureInfo.InvariantCulture, $"{EventTypes.ConnectionPolled}:{counts.Name}:{counts.Kind.Key}:{at.UtcTicks}"));
        _ = await ledger.Append(entry, lease, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Appends 'workflow.trigger.skipped' for a polled event that started no run. The key holds the workflow, the delivery id and the reason, so a repeat of the same skip writes one event.</summary>
    /// <param name="skipped">The workflow, the connection, the event kind, the delivery id and the reason.</param>
    /// <param name="grant">The lease to append under; null uses the one this process holds.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>A task that ends when the event is recorded or skipped.</returns>
    public async ValueTask TriggerSkipped(SkippedTrigger skipped, LeaseGrant? grant, CancellationToken cancellationToken)
    {
        if ((grant ?? held.Current) is not { } lease)
        {
            return;
        }

        var entry = events.Entry(EventTypes.WorkflowTriggerSkipped, new WorkflowTriggerSkipped(skipped.Workflow, skipped.Name.ToString(), skipped.Kind.Key, skipped.DeliveryKey, skipped.Reason, Now), $"{EventTypes.WorkflowTriggerSkipped}:{skipped.Workflow}:{skipped.DeliveryKey}:{skipped.Reason}");
        _ = await ledger.Append(entry, lease, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Appends 'catchup.capped' once for a capped read of (connection, event kind).</summary>
    /// <param name="name">The connection.</param>
    /// <param name="kind">The event kind.</param>
    /// <param name="reason">Why, in words for the owner.</param>
    /// <param name="grant">The lease to append under; null uses the one this process holds.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>A task that ends when the event is recorded or skipped.</returns>
    public async ValueTask CatchUpStopped(ConnectionName name, EventKind kind, string reason, LeaseGrant? grant, CancellationToken cancellationToken)
    {
        if ((grant ?? held.Current) is not { } lease)
        {
            return;
        }

        var at = Now;
        var entry = events.Entry(EventTypes.CatchUpCapped, new CatchUpCapped(name.ToString(), kind.Key, reason, at), string.Create(CultureInfo.InvariantCulture, $"{EventTypes.CatchUpCapped}:{name}:{kind.Key}:{at.UtcTicks}"));
        _ = await ledger.Append(entry, lease, cancellationToken).ConfigureAwait(false);
    }
}
