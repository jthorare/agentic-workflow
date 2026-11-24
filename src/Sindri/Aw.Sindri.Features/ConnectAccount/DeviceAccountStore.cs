using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.ConnectAccount;

/// <summary>
/// Stores a device-flow token as the `github:default` connection (first-run spec decision 14): the token goes only to <see cref="IConnectionSecrets"/> (the secret store), the row holds its pointer, and every
/// GitHub reader finds it through <see cref="ConnectionCredentials"/>. `review.credential` stays unset: the forge falls back to the connected row. `setup.skipAccounts` is cleared. Runs under the command gate.
/// </summary>
/// <param name="store">The Connections page's rows.</param>
/// <param name="secrets">The secret store for the token.</param>
/// <param name="gate">The command gate the write waits for.</param>
/// <param name="events">Records `connection.signed-in`.</param>
/// <param name="profile">Clears `setup.skipAccounts`.</param>
internal sealed class DeviceAccountStore(IConnectionStore store, IConnectionSecrets secrets, ICommandGate gate, ConnectionEvents events, AccountProfileWrites profile)
{
    /// <summary>Gets the connection a device-flow sign-in writes.</summary>
    internal static ConnectionName Name { get; } = ConnectionName.Parse("github:default").Value;

    /// <summary>Stores the token and writes the row.</summary>
    /// <param name="tokens">GitHub's tokens.</param>
    /// <param name="client">The client id the sign-in used (not secret; kept for refresh).</param>
    /// <param name="actor">Who connected.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Unit, or why nothing was stored (no secret is left without a row).</returns>
    internal async ValueTask<Result<Unit>> Save(TokenSet tokens, string client, string actor, CancellationToken cancellationToken)
    {
        using var held = await gate.Enter(cancellationToken).ConfigureAwait(false);
        if (held is null)
        {
            return SindriErrors.CommandBusy.With("another command held the command gate for 5 s; connect GitHub again");
        }

        var pointer = secrets.Store(Name, tokens);
        if (pointer.IsError)
        {
            return ConnectionFailures.Safe(pointer.Error);
        }

        var existing = await store.Get(Name, cancellationToken).ConfigureAwait(false);
        var now = events.Now;
        var config = new Dictionary<string, string>(StringComparer.Ordinal) { ["clientId"] = client, ["signedInAt"] = now.ToString("O", System.Globalization.CultureInfo.InvariantCulture) };
        var row = new Connection(Name, ConnectionKind.OAuth, "github", config, pointer.Value, ConnectionStatus.Connected, StatusReason: null,
            existing.IsSuccess ? existing.Value.CreatedBy : actor, existing.IsSuccess ? existing.Value.CreatedAt : now, now, now);
        var written = await store.Upsert(row, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            _ = secrets.Delete(pointer.Value);   // why: a secret with no row is one nobody can remove
            return ConnectionFailures.Safe(written.Error);
        }

        await events.SignedIn(Name, cancellationToken).ConfigureAwait(false);
        return await profile.ClearSkip(cancellationToken).ConfigureAwait(false);
    }
}
