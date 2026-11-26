using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.AddConnection;

/// <summary>
/// Adds a connection. With no --kind, a preset whose first way to connect is a key (<see cref="ConnectionPreset.KeyFirst"/>: github, linear, slack) makes a rest connection, the others the preset's kind (#4357). A bad name is SND-CONN-008, an existing one SND-CONN-010 (the insert itself refuses a duplicate, so two adds racing cannot both win), a preset
/// without a client id SND-CONN-007, and a preset that cannot take a loopback sign-in is refused with its sign-in note before any listener opens. An oauth connection
/// runs <see cref="OAuthSignInFlow"/>. A rest or cli connection stores the key the CLI sent (read from the CLI's own environment) or, for an agent, the variable named by
/// --key-env in serve's environment; a rest key for a preset is checked with its service first (<see cref="IKeyCheck"/>: SND-CONN-018 refused, SND-CONN-019 not reached, SND-CONN-020 rate limited, nothing stored). An error shows its code and fixed title only (<see cref="ConnectionFailures"/>).
/// </summary>
/// <param name="store">The connections.</param>
/// <param name="presets">The shipped presets.</param>
/// <param name="environment">Reads the OAuth client's variable when the caller sent only its name (serve's environment).</param>
/// <param name="callers">Who is adding.</param>
/// <param name="oauth">The oauth sign-in.</param>
/// <param name="keys">The key add (rest, cli and a keyed mcp).</param>
internal sealed class AddConnectionService(IConnectionStore store, IConnectionPresetCatalog presets, IEnvironmentReader environment, ICallerContextAccessor callers, OAuthSignInFlow oauth, KeyConnectionAdd keys)
    : IService<AddConnectionRequest, AddConnectionResponse>
{
    private readonly IConnectionStore _store = store;
    private readonly IConnectionPresetCatalog _presets = presets;
    private readonly IEnvironmentReader _environment = environment;
    private readonly ICallerContextAccessor _callers = callers;
    private readonly OAuthSignInFlow _oauth = oauth;
    private readonly KeyConnectionAdd _keys = keys;

    public async ValueTask<Result<AddConnectionResponse>> Handle(AddConnectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = ConnectionName.ParseUnreserved(request.Name);
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
            ConnectionKind.Rest or ConnectionKind.Cli => await _keys.Run(name.Value, kind.Value, request, preset.Value, cancellationToken).ConfigureAwait(false),
            ConnectionKind.Mcp => await AddMcp(name.Value, request, cancellationToken).ConfigureAwait(false),
            _ => SindriErrors.InvalidInput.With($"cannot add a connection of kind {ConnectionWire.Name(kind.Value)} (fix: use oauth, mcp, rest or cli)"),
        };
    }

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

        // #4357: a preset whose first way to connect is a key (github, linear, slack) stores a key unless --kind oauth asks for the browser sign-in
        return preset is null ? SindriErrors.InvalidInput.With("name a --preset or a --kind (oauth, rest or cli)")
            : preset.KeyFirst ? ConnectionKind.Rest
            : preset.Kind;
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

        var secret = KeyConnectionAdd.Secret(_environment, request.ClientSecret, request.ClientSecretEnv, "--client-secret-env");
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
            return await _keys.Run(name, ConnectionKind.Mcp, request, preset: null, cancellationToken).ConfigureAwait(false);
        }

        return await _oauth.RunMcp(new McpAdd(name, server, _callers.Current?.Actor ?? "unknown", request.Wait), cancellationToken).ConfigureAwait(false);
    }
}
