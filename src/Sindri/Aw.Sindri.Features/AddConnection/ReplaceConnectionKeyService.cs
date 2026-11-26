using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.AddConnection;

/// <summary>
/// Replaces the key of a stored rest or cli connection (#4358). The new key is read like an add reads it (the CLI's value, or the variable named by --key-env in serve's environment), trimmed, and for a rest connection with a
/// preset checked with its service first (<see cref="IKeyCheck"/>: SND-CONN-018 refused, SND-CONN-019 not reached, SND-CONN-020 rate limited). Until that check passes nothing is written, so a refused key leaves the old key in the keychain and
/// the row as it was. A good key overwrites the connection's keychain entry, and the row ends connected with no reason, and the answer says whether the key was checked (a cli connection, a rest one with no preset or a preset with no key check is stored unchecked, with the reason); a row that needed a sign-in clears its Needs-you row. A failed row write puts the old key back.
/// An unknown name is SND-CONN-001; a row whose preset left the catalog is SND-CLI-003 as an add of an unknown preset is (nothing is checked or written); an oauth connection, or an mcp one that signed in through its server, has a sign-in, not a key (SND-CLI-003 naming 'connection signin'); a keyed mcp connection (added with --key-env) takes the key unchecked; no key is SND-CLI-003. An error shows its code and fixed title only (<see cref="ConnectionFailures"/>).
/// </summary>
/// <param name="store">The connections.</param>
/// <param name="secrets">Stores the new key.</param>
/// <param name="presets">The shipped presets.</param>
/// <param name="environment">Reads --key-env when the caller sent only the name (serve's environment).</param>
/// <param name="events">Clears the Needs-you row and holds the clock.</param>
/// <param name="keys">Checks the key with its preset's service.</param>
internal sealed class ReplaceConnectionKeyService(IConnectionStore store, IConnectionSecrets secrets, IConnectionPresetCatalog presets, IEnvironmentReader environment, ConnectionEvents events, IKeyCheck keys)
    : IService<ReplaceConnectionKeyRequest, ReplaceConnectionKeyResponse>
{
    private readonly IConnectionStore _store = store;
    private readonly IConnectionSecrets _secrets = secrets;
    private readonly IConnectionPresetCatalog _presets = presets;
    private readonly IEnvironmentReader _environment = environment;
    private readonly ConnectionEvents _events = events;
    private readonly IKeyCheck _keys = keys;

    public async ValueTask<Result<ReplaceConnectionKeyResponse>> Handle(ReplaceConnectionKeyRequest request, CancellationToken cancellationToken)
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
        if (NoKey(row) is { } signIn)
        {
            return signIn;
        }

        var key = Key(request);
        if (key.IsError)
        {
            return key.Error;
        }

        var preset = Preset(row);
        if (preset.IsError)
        {
            return preset.Error;
        }

        var verdict = await Verdict(row, preset.Value, key.Value, cancellationToken).ConfigureAwait(false);
        return verdict.IsError ? verdict.Error : await Replace(row, key.Value, verdict.Value, cancellationToken).ConfigureAwait(false);
    }

    // The refusal for a connection that holds no key to replace, or null when it does: a rest or cli connection, or an mcp one added with --key-env. An mcp row says which at add (Config "auth": "key" or "signin",
    // #4482) and that mark wins. Only a row added before the mark (no "auth") falls back to the stored token: a keyed one is a bare access token (no refresh token, no expiry, as KeyConnectionAdd.Save writes it), while
    // a token from the server's own sign-in usually carries a refresh token or an expiry. The re-read in Fresh calls this again, so a mark written during the check counts too.
    private Error? NoKey(Connection row)
    {
        var keyed = row.Kind is ConnectionKind.Rest or ConnectionKind.Cli || (row.Kind == ConnectionKind.Mcp && McpHoldsKey(row));
        return keyed ? null : SindriErrors.InvalidInput.With($"{row.Name} signs in through the browser and holds no key (fix: sindri connection signin {row.Name})");
    }

    private bool McpHoldsKey(Connection row) => row.Config.TryGetValue(ConnectionAuth.Key, out var auth)
        ? string.Equals(auth, ConnectionAuth.Keyed, StringComparison.Ordinal)
        : row.SecretPointer is { } pointer && _secrets.Read(pointer) is { IsSuccess: true } read && read.Value.RefreshToken is null && read.Value.ExpiresAt is null;

    // Asks the service before anything is written (a refused or unreachable key leaves the old key and the row alone). Null when the key passed a real check, else why it went unchecked.
    // why: a preset with no key check answers NotApplicable without calling anything, so "passed" would be a claim nobody made.
    private async ValueTask<Result<string?>> Verdict(Connection row, ConnectionPreset? preset, string key, CancellationToken cancellationToken)
    {
        if (row.Kind != ConnectionKind.Rest || preset is null)
        {
            return UncheckedReason(row, preset);
        }

        var outcome = await _keys.Check(preset, key, cancellationToken).ConfigureAwait(false);
        if (outcome.IsError)
        {
            return outcome.Error;
        }

        return outcome.Value == KeyCheckOutcome.Checked ? Results.Success<string?>(value: null) : UncheckedReason(row, preset);
    }

    // The row's preset; a preset that left the catalog is refused as an add refuses an unknown one, so the key never goes unchecked by accident.
    private Result<ConnectionPreset?> Preset(Connection row)
    {
        if (row.Preset is not { } key)
        {
            return Results.Success<ConnectionPreset?>(value: null);
        }

        var found = _presets.Find(key);
        if (found is not null)
        {
            return found;
        }

        var shipped = _presets.List();
        var names = shipped.IsSuccess ? string.Join(", ", shipped.Value.Select(static p => p.Key)) : "none";
        return SindriErrors.InvalidInput.With($"no connection preset named '{key}' (shipped: {names})");
    }

    private static string UncheckedReason(Connection row, ConnectionPreset? preset) => row.Kind == ConnectionKind.Cli
        ? "a cli connection has no service to check the key against"
        : row.Kind == ConnectionKind.Mcp ? "an mcp connection has no key check"
        : preset is null ? "this connection has no preset, so there is no service to check the key against" : $"{preset.DisplayName} has no key check";

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

    // The row as it is now. The key check can take 15 s, during which a poller, a refresh or a rename may have written the row, so the write below starts from this read and changes only its own fields. The store has no
    // version to compare and set, so the re-read is the guard; a row that is gone (SND-CONN-001) or that no longer holds a key is refused before the keychain entry is touched, so there is nothing to put back.
    private async ValueTask<Result<Connection>> Fresh(Connection row, CancellationToken cancellationToken)
    {
        var again = await _store.Get(row.Name, cancellationToken).ConfigureAwait(false);
        if (again.IsError)
        {
            return ConnectionFailures.Safe(again.Error);
        }

        return NoKey(again.Value) is { } refused ? refused : again.Value;
    }

    // Overwrites the keychain entry, then the row; a failed row write puts the old key back.
    private async ValueTask<Result<ReplaceConnectionKeyResponse>> Replace(Connection checkedRow, string key, string? uncheckedReason, CancellationToken cancellationToken)
    {
        var current = await Fresh(checkedRow, cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return current.Error;
        }

        var row = current.Value;
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

            // why: when the old key could not be read (old is null) there is nothing to put back, so the new key stays in the keychain beside a row that still says what it said; a retry replaces it again

            return ConnectionFailures.Safe(written.Error);
        }

        if (row.Status == ConnectionStatus.NeedsSignIn)
        {
            await _events.SignedIn(row.Name, cancellationToken).ConfigureAwait(false);
        }

        return new ReplaceConnectionKeyResponse(ConnectionView.From(updated), Checked: uncheckedReason is null, uncheckedReason);
    }
}
