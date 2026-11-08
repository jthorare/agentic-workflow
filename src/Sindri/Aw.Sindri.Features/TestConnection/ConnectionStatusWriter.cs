using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.TestConnection;

/// <summary>
/// The connection test's one write (#2349): the test reads the secret and probes the provider outside the command gate; this waits for the gate (5 s, then SND-API-009
/// like any command), re-reads the connection so a removal or edit made during the probe isn't overwritten, and records the status.
/// </summary>
/// <param name="store">The connections.</param>
/// <param name="events">The clock, and the 'connection.signed-in' event that clears a Needs-you row once a test passes again.</param>
/// <param name="gate">The command gate.</param>
internal sealed class ConnectionStatusWriter(IConnectionStore store, ConnectionEvents events, ICommandGate gate)
{
    /// <summary>Records <paramref name="status"/> on the connection named <paramref name="name"/>.</summary>
    /// <param name="name">The connection.</param>
    /// <param name="status">The new status.</param>
    /// <param name="reason">The reason shown for a failed test; ignored when <paramref name="ok"/>.</param>
    /// <param name="ok">True when the test passed.</param>
    /// <param name="cancellationToken">Ends the wait and the write.</param>
    /// <returns>The updated connection, or SND-API-009 when the gate stayed busy.</returns>
    public async ValueTask<Result<Connection>> Record(ConnectionName name, ConnectionStatus status, string? reason, bool ok, CancellationToken cancellationToken)
    {
        using var held = await gate.Enter(cancellationToken).ConfigureAwait(false);
        if (held is null)
        {
            return SindriErrors.CommandBusy.With("another command held the command gate for 5 s; try again in a moment");
        }

        var current = await store.Get(name, cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return ConnectionFailures.Safe(current.Error);
        }

        var updated = current.Value with { Status = status, StatusReason = ok ? null : reason, UpdatedAt = events.Now };
        var written = await store.Update(updated, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return ConnectionFailures.Safe(written.Error);
        }

        if (ok && current.Value.Status == ConnectionStatus.NeedsSignIn)
        {
            await events.SignedIn(name, cancellationToken).ConfigureAwait(false);   // the Needs-you row of the failed refresh goes away
        }

        return updated;
    }
}
