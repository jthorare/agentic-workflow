using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.ConnectAccount;

/// <summary>
/// Undoes what the accounts step wrote for GitHub: ends a device sign-in in progress, removes the `github:gh` and `github:default` rows (and the device token from the secret store), and removes
/// `review.credential` when it is the gh command. A GitHub connection the owner added by hand on the Connections page is left alone. Owner only (SND-AUTH-002). Answers `disconnected`.
/// </summary>
/// <param name="callers">The caller.</param>
/// <param name="device">The device sign-in in progress.</param>
/// <param name="store">The Connections page's rows.</param>
/// <param name="secrets">The secret store.</param>
/// <param name="events">Records `connection.removed`.</param>
/// <param name="profile">Removes `review.credential`.</param>
internal sealed class DisconnectAccountService(ICallerContextAccessor callers, DeviceAccountSignIn device, IConnectionStore store, IConnectionSecrets secrets, ConnectionEvents events, AccountProfileWrites profile)
    : IService<DisconnectAccountRequest, ConnectAccountResponse>
{
    public async ValueTask<Result<ConnectAccountResponse>> Handle(DisconnectAccountRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (OwnerCaller.Refusal(callers, "Disconnecting an account") is { } refused)
        {
            return refused;
        }

        if (!string.Equals(request.Provider, "github", StringComparison.Ordinal))
        {
            return SindriErrors.InvalidInput.With($"'{request.Provider}' is not an account Sindri can disconnect yet: github");
        }

        _ = device.Cancel();
        foreach (var name in (ConnectionName[])[GitHubCliCredential.ConnectionName, DeviceAccountStore.Name])
        {
            if (await Remove(name, cancellationToken).ConfigureAwait(false) is { } failed)
            {
                return failed;
            }
        }

        var cleared = await profile.ClearGhCredential(cancellationToken).ConfigureAwait(false);
        return cleared.IsError ? cleared.Error : new ConnectAccountResponse(request.Provider, "disconnected");
    }

    // The secret goes first: a row whose secret is gone reads as needing sign-in, a secret with no row is one nobody can remove.
    private async ValueTask<Error?> Remove(ConnectionName name, CancellationToken cancellationToken)
    {
        var found = await store.Get(name, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return null;   // not there: nothing to remove
        }

        if (found.Value.SecretPointer is { } pointer && secrets.Delete(pointer) is { IsError: true } kept)
        {
            return ConnectionFailures.Safe(kept.Error);
        }

        var removed = await store.Remove(name, cancellationToken).ConfigureAwait(false);
        if (removed.IsError)
        {
            return removed.Error;
        }

        await events.Removed(name, cancellationToken).ConfigureAwait(false);
        return null;
    }
}
