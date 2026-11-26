using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.AddConnection;

/// <summary>
/// Replaces the key of a stored rest or cli connection (#4358). The new key is read like an add reads it (the CLI's value, or the variable named by --key-env in serve's environment), trimmed, and for a rest connection with a
/// preset checked with its service first (<see cref="IKeyCheck"/>: SND-CONN-018 refused, SND-CONN-019 not reached, SND-CONN-020 rate limited). Until that check passes nothing is written, so a refused key leaves the old key in the keychain and
/// the row as it was. A good key overwrites the connection's keychain entry, and the row ends connected with no reason; a row that needed a sign-in clears its Needs-you row. A failed row write puts the old key back.
/// An unknown name is SND-CONN-001; an oauth or mcp connection has a sign-in, not a key (SND-CLI-003 naming 'connection signin'); no key is SND-CLI-003. An error shows its code and fixed title only (<see cref="ConnectionFailures"/>).
/// </summary>
/// <param name="store">The connections.</param>
/// <param name="secrets">Stores the new key.</param>
/// <param name="presets">The shipped presets.</param>
/// <param name="environment">Reads --key-env when the caller sent only the name (serve's environment).</param>
/// <param name="events">Clears the Needs-you row and holds the clock.</param>
/// <param name="keys">Checks the key with its preset's service.</param>
internal sealed class ReplaceConnectionKeyService(IConnectionStore store, IConnectionSecrets secrets, IConnectionPresetCatalog presets, IEnvironmentReader environment, ConnectionEvents events, IKeyCheck keys)
    : IService<ReplaceConnectionKeyRequest, AddConnectionResponse>
{
    private readonly IConnectionStore _store = store;
    private readonly IConnectionSecrets _secrets = secrets;
    private readonly IConnectionPresetCatalog _presets = presets;
    private readonly IEnvironmentReader _environment = environment;
    private readonly ConnectionEvents _events = events;
    private readonly IKeyCheck _keys = keys;

    public async ValueTask<Result<AddConnectionResponse>> Handle(ReplaceConnectionKeyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = ConnectionName.Parse(request.Name);
        if (name.IsError)
        {
            return name.Error;
        }

        var found = await _store.Get(name.Value, cancellationToken).ConfigureAwait(false);
        if (found.IsError)
        {
            return ConnectionFailures.Safe(found.Error);
        }

        var row = found.Value;
        if (row.Kind is not (ConnectionKind.Rest or ConnectionKind.Cli))
        {
            return SindriErrors.InvalidInput.With($"{row.Name} signs in through the browser and holds no key (fix: sindri connection signin {row.Name})");
        }

        var key = Key(request);
        if (key.IsError)
        {
            return key.Error;
        }

        // the new key is asked of its service before anything is written: a refused or unchecked key leaves the old key and the row alone
        var preset = row.Preset is { } presetKey ? _presets.Find(presetKey) : null;
        if (row.Kind == ConnectionKind.Rest && preset is not null && await _keys.Check(preset, key.Value, cancellationToken).ConfigureAwait(false) is { IsError: true } refused)
        {
            return refused.Error;
        }

        return await Replace(row, key.Value, cancellationToken).ConfigureAwait(false);
    }

    // The new key, trimmed; no key or only whitespace is SND-CLI-003.
    private Result<string> Key(ReplaceConnectionKeyRequest request)
    {
        var read = KeyConnectionAdd.Secret(_environment, request.Key, request.KeyEnv, "--key-env");
        if (read.IsError)
        {
            return read.Error;
        }

        return read.Value?.Trim() is { Length: > 0 } key
            ? key
            : SindriErrors.InvalidInput.With("a new key is needed: pass --key-env NAME (the environment variable that holds the new key)");
    }

    // Overwrites the keychain entry, then the row; a failed row write puts the old key back.
    private async ValueTask<Result<AddConnectionResponse>> Replace(Connection row, string key, CancellationToken cancellationToken)
    {
        var old = row.SecretPointer is { } pointer && _secrets.Read(pointer) is { IsSuccess: true } read ? read.Value : null;
        var stored = _secrets.Store(row.Name, new TokenSet(key, RefreshToken: null, ExpiresAt: null));
        if (stored.IsError)
        {
            return ConnectionFailures.Safe(stored.Error);
        }

        var updated = row with { SecretPointer = stored.Value, Status = ConnectionStatus.Connected, StatusReason = null, UpdatedAt = _events.Now };
        var written = await _store.Update(updated, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            if (old is not null)
            {
                _ = _secrets.Store(row.Name, old);
            }

            return ConnectionFailures.Safe(written.Error);
        }

        if (row.Status == ConnectionStatus.NeedsSignIn)
        {
            await _events.SignedIn(row.Name, cancellationToken).ConfigureAwait(false);
        }

        return new AddConnectionResponse(ConnectionView.From(updated), ConsentUrl: null);
    }
}
