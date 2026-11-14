using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.AddConnection;

/// <summary>
/// Adds a connection. A bad name is SND-CONN-008, an existing one SND-CONN-010 (the insert itself refuses a duplicate, so two adds racing cannot both win), a preset
/// without a client id SND-CONN-007, and a preset that cannot take a loopback sign-in is refused with its sign-in note before any listener opens. An oauth connection
/// runs <see cref="OAuthSignInFlow"/>. A rest or cli connection stores the key the CLI sent (read from the CLI's own environment) or, for an agent, the variable named by
/// --key-env in serve's environment. An error shows its code and fixed title only (<see cref="ConnectionFailures"/>).
/// </summary>
/// <param name="store">The connections.</param>
/// <param name="secrets">Stores the tokens.</param>
/// <param name="presets">The shipped presets.</param>
/// <param name="environment">Reads --key-env and --client-secret-env when the caller sent only the names (serve's environment).</param>
/// <param name="time">The clock.</param>
/// <param name="callers">Who is adding.</param>
/// <param name="oauth">The oauth sign-in.</param>
internal sealed class AddConnectionService(IConnectionStore store, IConnectionSecrets secrets, IConnectionPresetCatalog presets, IEnvironmentReader environment, TimeProvider time, ICallerContextAccessor callers, OAuthSignInFlow oauth)
    : IService<AddConnectionRequest, AddConnectionResponse>
{
    private readonly IConnectionStore _store = store;
    private readonly IConnectionSecrets _secrets = secrets;
    private readonly IConnectionPresetCatalog _presets = presets;
    private readonly IEnvironmentReader _environment = environment;
    private readonly TimeProvider _time = time;
    private readonly ICallerContextAccessor _callers = callers;
    private readonly OAuthSignInFlow _oauth = oauth;

    public async ValueTask<Result<AddConnectionResponse>> Handle(AddConnectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = Named(request.Name);
        if (name.IsError)
        {
            return name.Error;
        }

        var existing = await _store.Get(name.Value, cancellationToken).ConfigureAwait(false);
        if (existing.IsSuccess)
        {
            return SindriErrors.ConnectionExists.With($"a connection named {name.Value} already exists (fix: sindri connection remove {name.Value} first)");
        }

        if (existing.Error.Code != SindriErrors.ConnectionNotFound.Code)
        {
            return ConnectionFailures.Safe(existing.Error);
        }

        var preset = Preset(request.Preset);
        if (preset.IsError)
        {
            return preset.Error;
        }

        var kind = Kind(request.Kind, preset.Value);
        if (kind.IsError)
        {
            return kind.Error;
        }

        return kind.Value switch
        {
            ConnectionKind.OAuth => await AddOAuth(name.Value, request, preset.Value, cancellationToken).ConfigureAwait(false),
            ConnectionKind.Rest or ConnectionKind.Cli => await AddKey(name.Value, kind.Value, request, preset.Value, cancellationToken).ConfigureAwait(false),
            ConnectionKind.Mcp => await AddMcp(name.Value, request, cancellationToken).ConfigureAwait(false),
            _ => SindriErrors.InvalidInput.With($"cannot add a connection of kind {ConnectionWire.Name(kind.Value)} (fix: use oauth, mcp, rest or cli)"),
        };
    }

    // slack:messages has no preset on purpose and is made only by sindri messages connect, so the generic add refuses the name (#3279).
    private static Result<ConnectionName> Named(string text) =>
        string.Equals(text, "slack:messages", StringComparison.Ordinal)
            ? SindriErrors.ConnectionNameReserved.With("slack:messages is made by sindri messages connect (fix: run sindri messages connect)")
            : ConnectionName.Parse(text);

    private Result<ConnectionPreset?> Preset(string? key)
    {
        if (key is null)
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

    private static Result<ConnectionKind> Kind(string? wire, ConnectionPreset? preset)
    {
        if (wire is not null)
        {
            return ConnectionWire.ParseKind(wire);
        }

        return preset is not null
            ? preset.Kind
            : SindriErrors.InvalidInput.With("name a --preset or a --kind (oauth, rest or cli)");
    }

    private async ValueTask<Result<AddConnectionResponse>> AddOAuth(ConnectionName name, AddConnectionRequest request, ConnectionPreset? preset, CancellationToken cancellationToken)
    {
        if (preset is null)
        {
            return SindriErrors.InvalidInput.With("an oauth connection needs a --preset (its endpoints come from the preset)");
        }

        if (!preset.LoopbackAllowed)
        {
            // why: the provider refuses a plain-http 127.0.0.1 redirect, so a consent page would open that can never come back; the note says how to sign in instead
            return SindriErrors.InvalidInput.With($"{preset.DisplayName} does not accept the browser sign-in Sindri runs on this machine. {preset.SignInNote}");
        }

        if (string.IsNullOrWhiteSpace(request.Client))
        {
            return SindriErrors.ConnectionNeedsClientId.With($"the {preset.Key} preset ships no client id (fix: register an OAuth app with {preset.DisplayName} and pass --client-id; {preset.SignInNote})");
        }

        var secret = Secret(request.ClientSecret, request.ClientSecretEnv, "--client-secret-env");
        if (secret.IsError)
        {
            return secret.Error;
        }

        var add = new OAuthAdd(name, preset, request.Client, secret.Value, request.Scopes, _callers.Current?.Actor ?? "unknown", request.Wait);
        return await _oauth.Run(add, cancellationToken).ConfigureAwait(false);
    }

    // An mcp connection takes --url and either --key-env (a key, stored like a rest one) or no key (the server's own OAuth sign-in through ISignInSession.StartMcp,
    // with a client registered on the spot). The row's config holds 'serverUrl'.
    private async ValueTask<Result<AddConnectionResponse>> AddMcp(ConnectionName name, AddConnectionRequest request, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var server) || !(server.Scheme == Uri.UriSchemeHttps || (server.Scheme == Uri.UriSchemeHttp && server.IsLoopback)))
        {
            return SindriErrors.InvalidInput.With("an mcp connection needs --url with the server's address (https, or http on this machine)");
        }

        if (request.Key is { Length: > 0 } || !string.IsNullOrWhiteSpace(request.KeyEnv))
        {
            return await AddKey(name, ConnectionKind.Mcp, request, preset: null, cancellationToken).ConfigureAwait(false);
        }

        return await _oauth.RunMcp(new McpAdd(name, server, _callers.Current?.Actor ?? "unknown", request.Wait), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<AddConnectionResponse>> AddKey(ConnectionName name, ConnectionKind kind, AddConnectionRequest request, ConnectionPreset? preset, CancellationToken cancellationToken)
    {
        var key = Secret(request.Key, request.KeyEnv, "--key-env");
        if (key.IsError)
        {
            return key.Error;
        }

        if (key.Value is null)
        {
            return SindriErrors.InvalidInput.With($"a {ConnectionWire.Name(kind)} connection needs --key-env NAME (the environment variable that holds the key)");
        }

        var pointer = _secrets.Store(name, new TokenSet(key.Value, RefreshToken: null, ExpiresAt: null));
        if (pointer.IsError)
        {
            return ConnectionFailures.Safe(pointer.Error);
        }

        var now = _time.GetUtcNow();
        var row = new Connection(name, kind, preset?.Key, KeyConfig(kind, preset, request), pointer.Value, ConnectionStatus.Connected, StatusReason: null, _callers.Current?.Actor ?? "unknown", now, now, LastRefreshedAt: null);
        var written = await _store.Insert(row, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            _ = _secrets.Delete(pointer.Value);
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

    // The value the CLI sent wins; otherwise the variable named by the flag is read from serve's environment (an agent has no other way). Neither given is no secret; a name that is unset is an error that says whose environment.
    private Result<string?> Secret(string? sent, string? variable, string flag)
    {
        if (sent is { Length: > 0 })
        {
            return sent;
        }

        if (string.IsNullOrWhiteSpace(variable))
        {
            return Results.Success<string?>(value: null);
        }

        return _environment.Get(variable) is { } read
            ? read
            : SindriErrors.InvalidInput.With($"the environment variable {variable} is not set or is empty in the environment of the running Sindri (fix: export it where `sindri` runs, or run `sindri connection add ... {flag} {variable}` from a terminal, which sends its own value)");
    }
}
