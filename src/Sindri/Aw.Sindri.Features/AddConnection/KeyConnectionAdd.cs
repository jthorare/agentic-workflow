using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.AddConnection;

/// <summary>
/// The key half of <see cref="AddConnectionService"/>: a rest, cli or keyed mcp connection. It takes the key the CLI sent (read from the CLI's own environment) or, for an agent, the variable named by --key-env
/// in serve's environment. The key is trimmed of whitespace (a paste's trailing newline). A rest key for a preset is checked with its service first (#4357, <see cref="IKeyCheck"/>: SND-CONN-018 refused, SND-CONN-019 not reached, SND-CONN-020 rate limited); only then is it stored and the row
/// written as connected. A failed insert deletes the stored secret again.
/// </summary>
/// <param name="store">The connections.</param>
/// <param name="secrets">Stores the key.</param>
/// <param name="environment">Reads --key-env when the caller sent only the name (serve's environment).</param>
/// <param name="time">The clock.</param>
/// <param name="callers">Who is adding.</param>
/// <param name="keys">Checks the key with its preset's service.</param>
internal sealed class KeyConnectionAdd(IConnectionStore store, IConnectionSecrets secrets, IEnvironmentReader environment, TimeProvider time, ICallerContextAccessor callers, IKeyCheck keys)
{
    /// <summary>Adds the key connection.</summary>
    /// <param name="name">The checked name.</param>
    /// <param name="kind">rest, cli or mcp.</param>
    /// <param name="request">The add.</param>
    /// <param name="preset">The preset, or null for a hand-made connection.</param>
    /// <param name="cancellationToken">Cancels the check and the write.</param>
    /// <returns>The connected connection, or the error (no key in its text).</returns>
    internal async ValueTask<Result<AddConnectionResponse>> Run(ConnectionName name, ConnectionKind kind, AddConnectionRequest request, ConnectionPreset? preset, CancellationToken cancellationToken)
    {
        var key = Secret(environment, request.Key, request.KeyEnv, "--key-env");
        if (key.IsError)
        {
            return key.Error;
        }

        // a pasted key often ends in a newline: the check and the store both see it trimmed, and a key of only whitespace is no key
        var pasted = key.Value?.Trim();
        if (string.IsNullOrEmpty(pasted))
        {
            return NoKey(kind, preset);
        }

        // a rest key for a preset is asked of its service first: a refused or unchecked key is never stored and no row says connected
        if (kind == ConnectionKind.Rest && preset is not null && await keys.Check(preset, pasted, cancellationToken).ConfigureAwait(false) is { IsError: true } refused)
        {
            return refused.Error;
        }

        var now = time.GetUtcNow();
        var row = new Connection(name, kind, preset?.Key, KeyConfig(kind, preset, request), SecretPointer: null, ConnectionStatus.Connected, StatusReason: null, callers.Current?.Actor ?? "unknown", now, now, LastRefreshedAt: null);
        return await Save(row, pasted, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>The value the CLI sent wins; otherwise the variable named by the flag is read from serve's environment (an agent has no other way). Neither given is no value; a name that is unset is an error that says whose environment.</summary>
    /// <param name="environment">Serve's environment.</param>
    /// <param name="sent">The value the CLI sent, or null.</param>
    /// <param name="variable">The variable's name, or null.</param>
    /// <param name="flag">The flag that names the variable, for the error.</param>
    /// <returns>The value, null for none, or the error.</returns>
    internal static Result<string?> Secret(IEnvironmentReader environment, string? sent, string? variable, string flag)
    {
        if (sent is { Length: > 0 })
        {
            return sent;
        }

        if (string.IsNullOrWhiteSpace(variable))
        {
            return Results.Success<string?>(value: null);
        }

        return environment.Get(variable) is { } read
            ? read
            : SindriErrors.InvalidInput.With($"the environment variable {variable} is not set or is empty in the environment of the running Sindri (fix: export it where `sindri` runs, or run `sindri connection add ... {flag} {variable}` from a terminal, which sends its own value)");
    }

    // The add named no key, or one of only whitespace.
    private static Error NoKey(ConnectionKind kind, ConnectionPreset? preset) =>
        SindriErrors.InvalidInput.With(preset is { KeyFirst: true }
            ? $"a {preset.DisplayName} connection takes a key: pass --key-env NAME (the environment variable that holds your API key or personal access token), or --kind oauth for a browser sign-in with your own OAuth app"
            : $"a {ConnectionWire.Name(kind)} connection needs --key-env NAME (the environment variable that holds the key)");

    // Stores the key, then writes the row pointing at it; a failed insert deletes the key again.
    private async ValueTask<Result<AddConnectionResponse>> Save(Connection draft, string key, CancellationToken cancellationToken)
    {
        var pointer = secrets.Store(draft.Name, new TokenSet(key, RefreshToken: null, ExpiresAt: null));
        if (pointer.IsError)
        {
            return ConnectionFailures.Safe(pointer.Error);
        }

        var row = draft with { SecretPointer = pointer.Value };
        var written = await store.Insert(row, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            _ = secrets.Delete(pointer.Value);
            return ConnectionFailures.Safe(written.Error);
        }

        return new AddConnectionResponse(ConnectionView.From(row), ConsentUrl: null);
    }

    // A probe URL from the preset, and for an mcp connection the server's address (AddMcp checked that it parses).
    private static Dictionary<string, string> KeyConfig(ConnectionKind kind, ConnectionPreset? preset, AddConnectionRequest request)
    {
        var config = new Dictionary<string, string>(StringComparer.Ordinal);
        if (preset?.ProbeUrl is { } probe)
        {
            config["probeUrl"] = probe;
        }

        if (kind == ConnectionKind.Mcp && request.Url is { } server)
        {
            config["serverUrl"] = new Uri(server).ToString();
        }

        return config;
    }
}
