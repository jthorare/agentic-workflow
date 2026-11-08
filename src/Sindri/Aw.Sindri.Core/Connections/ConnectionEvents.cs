using System.Globalization;
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
}
