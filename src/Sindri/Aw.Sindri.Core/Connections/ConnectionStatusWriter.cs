using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Core.Connections;

/// <summary>
/// The writes a connection gets after a slow call outside the command gate (a test probe or a token refresh): each waits for the gate (5 s, then SND-API-009 like any command),
/// re-reads the connection and writes only if the row is still the one the call started from (same secret pointer, last-refreshed time and update time). A row changed meanwhile
/// (a sign-in, a refresh, an edit) is left as it is now. A removal made during the call is SND-CONN-001 and stores nothing. Every move into 'needs-sign-in' goes through
/// <see cref="Record"/>, which appends the 'connection.needs-sign-in' event (the Needs-you row) after the row is written, once per move.
/// </summary>
/// <param name="store">The connections.</param>
/// <param name="secrets">Reads and replaces the tokens a refresh fetched.</param>
/// <param name="events">The clock, and the events that add and clear a Needs-you row.</param>
/// <param name="gate">The command gate.</param>
public sealed class ConnectionStatusWriter(IConnectionStore store, IConnectionSecrets secrets, ConnectionEvents events, ICommandGate gate)
{
    private const string Busy = "another command held the command gate for 5 s; try again in a moment";

    /// <summary>Records <paramref name="status"/> on the connection <paramref name="probed"/> was read as. A passed test is <see cref="ConnectionStatus.Connected"/>, which clears the reason.</summary>
    /// <param name="probed">The connection as the call started from it.</param>
    /// <param name="status">The new status.</param>
    /// <param name="reason">The reason shown for a failed test or refresh; dropped when the status is connected.</param>
    /// <param name="grant">The lease the 'needs-sign-in' event is fenced by; null uses the one this process holds.</param>
    /// <param name="cancellationToken">Ends the wait and the write.</param>
    /// <returns>The connection as it is now (updated, or as found when it changed during the call), or SND-API-009 when the gate stayed busy.</returns>
    public async ValueTask<Result<Connection>> Record(Connection probed, ConnectionStatus status, string? reason, LeaseGrant? grant, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(probed);
        using var held = await gate.Enter(cancellationToken).ConfigureAwait(false);
        if (held is null)
        {
            return SindriErrors.CommandBusy.With(Busy);
        }

        var current = await store.Get(probed.Name, cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return ConnectionFailures.Safe(current.Error);
        }

        if (!Unchanged(probed, current.Value))
        {
            return current.Value;   // a sign-in or refresh landed during the call: its result is newer than ours
        }

        var ok = status == ConnectionStatus.Connected;
        var updated = current.Value with { Status = status, StatusReason = ok ? null : reason, UpdatedAt = events.Now };
        var written = await store.Update(updated, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            return ConnectionFailures.Safe(written.Error);
        }

        await Announce(current.Value.Status, updated, grant, cancellationToken).ConfigureAwait(false);
        return updated;
    }

    /// <summary>
    /// Stores the tokens a refresh fetched and stamps the row, only if the row and the stored secret are still what the refresh started from.
    /// </summary>
    /// <param name="snapshot">The connection as the refresh started from it.</param>
    /// <param name="old">The tokens the refresh presented.</param>
    /// <param name="fresh">The tokens the provider returned.</param>
    /// <param name="cancellationToken">Ends the wait and the write.</param>
    /// <returns>True when the new tokens were stored; false when the gate stayed busy, the row was removed or a sign-in or test changed the row or its secret meanwhile (nothing is stored); the store's error otherwise.</returns>
    public async ValueTask<Result<bool>> ReplaceTokens(Connection snapshot, TokenSet old, TokenSet fresh, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(old);
        ArgumentNullException.ThrowIfNull(fresh);
        using var held = await gate.Enter(cancellationToken).ConfigureAwait(false);
        if (held is null)
        {
            return false;   // the tokens just fetched are dropped and the next tick refreshes again
        }

        var current = await store.Get(snapshot.Name, cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return current.Error.Code == SindriErrors.ConnectionNotFound.Code ? false : ConnectionFailures.Safe(current.Error);   // removed during the call: nothing is stored
        }

        if (!Unchanged(snapshot, current.Value) || current.Value.SecretPointer is not { } pointer || !SameTokens(secrets.Read(pointer), old))
        {
            return false;
        }

        return await Store(current.Value, old, fresh, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>True when <paramref name="now"/> is still the row a call started from: nothing that a sign-in, a refresh or an edit changes is different.</summary>
    /// <param name="before">The connection when the work started.</param>
    /// <param name="now">The connection as stored now.</param>
    /// <returns>Whether the write may go ahead.</returns>
    public static bool Unchanged(Connection before, Connection now)
    {
        ArgumentNullException.ThrowIfNull(before);
        ArgumentNullException.ThrowIfNull(now);
        return Equals(before.SecretPointer, now.SecretPointer) && before.LastRefreshedAt == now.LastRefreshedAt && before.UpdatedAt == now.UpdatedAt;
    }

    private async ValueTask<Result<bool>> Store(Connection current, TokenSet old, TokenSet fresh, CancellationToken cancellationToken)
    {
        // a provider that sends no new refresh token keeps the old one; the client secret is stored with the tokens and must survive every refresh
        var stored = secrets.Store(current.Name, fresh with { RefreshToken = fresh.RefreshToken ?? old.RefreshToken, ClientSecret = old.ClientSecret });
        if (stored.IsError)
        {
            return ConnectionFailures.Safe(stored.Error);
        }

        var now = events.Now;
        var written = await store.Update(current with { SecretPointer = stored.Value, LastRefreshedAt = now, UpdatedAt = now }, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            if (written.Error.Code == SindriErrors.ConnectionNotFound.Code)
            {
                _ = secrets.Delete(stored.Value);   // why: a secret with no row is one nobody can remove
            }

            return ConnectionFailures.Safe(written.Error);
        }

        return true;
    }

    private static bool SameTokens(Result<TokenSet> read, TokenSet old)
        => read.IsSuccess && string.Equals(read.Value.AccessToken, old.AccessToken, StringComparison.Ordinal) && string.Equals(read.Value.RefreshToken, old.RefreshToken, StringComparison.Ordinal);

    // The Needs-you row follows the status: a connection that passes again clears it, one that newly needs a sign-in gets it (once, not on every repeat).
    private async ValueTask Announce(ConnectionStatus before, Connection updated, LeaseGrant? grant, CancellationToken cancellationToken)
    {
        if (updated.Status == ConnectionStatus.Connected && before == ConnectionStatus.NeedsSignIn)
        {
            await events.SignedIn(updated.Name, cancellationToken).ConfigureAwait(false);
        }
        else if (updated.Status == ConnectionStatus.NeedsSignIn && before != ConnectionStatus.NeedsSignIn)
        {
            await events.NeedsSignIn(updated.Name, updated.StatusReason ?? "sign in again", grant, cancellationToken).ConfigureAwait(false);
        }
    }
}
