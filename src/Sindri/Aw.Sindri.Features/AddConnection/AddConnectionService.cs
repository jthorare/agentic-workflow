using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;

namespace Aw.Sindri.Features.AddConnection;

/// <summary>
/// Adds a connection. A bad name is SND-CONN-008, an existing one SND-CONN-010, a preset without a client id SND-CONN-007. An oauth connection starts the
/// sign-in; its row is written only after the tokens are stored, so a denied consent (SND-CONN-009), a timeout (SND-CONN-003) or a state mismatch
/// (SND-CONN-004) leaves no row and no secret. A rest or cli connection reads --key-env once and stores it as the secret. An error shows its code and
/// fixed title only (<see cref="ConnectionFailures"/>),.
/// </summary>
/// <param name="store">The connections.</param>
/// <param name="secrets">Stores the tokens.</param>
/// <param name="presets">The shipped presets.</param>
/// <param name="signIn">Starts the browser sign-in.</param>
/// <param name="environment">Reads --key-env.</param>
/// <param name="time">The clock.</param>
/// <param name="callers">Who is adding.</param>
internal sealed class AddConnectionService(IConnectionStore store, IConnectionSecrets secrets, IConnectionPresetCatalog presets, ISignInSession signIn, IEnvironmentReader environment, TimeProvider time, ICallerContextAccessor callers)
    : IService<AddConnectionRequest, AddConnectionResponse>
{
    private readonly IConnectionStore _store = store;
    private readonly IConnectionSecrets _secrets = secrets;
    private readonly IConnectionPresetCatalog _presets = presets;
    private readonly ISignInSession _signIn = signIn;
    private readonly IEnvironmentReader _environment = environment;
    private readonly TimeProvider _time = time;
    private readonly ICallerContextAccessor _callers = callers;

    public async ValueTask<Result<AddConnectionResponse>> Handle(AddConnectionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = ConnectionName.Parse(request.Name);
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
            _ => SindriErrors.InvalidInput.With("the mcp connection kind arrives in a later release (fix: use --kind rest or --kind cli with --key-env)"),
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

        if (string.IsNullOrWhiteSpace(request.Client))
        {
            return SindriErrors.ConnectionNeedsClientId.With($"the {preset.Key} preset ships no client id (fix: register an OAuth app with {preset.DisplayName} and pass --client-id; {preset.SignInNote})");
        }

        var started = await _signIn.Start(preset, request.Client, request.Scopes, cancellationToken).ConfigureAwait(false);
        if (started.IsError)
        {
            return ConnectionFailures.Safe(started.Error);
        }

        var sign = new SignInContext(name, preset, request.Client, request.Scopes.Count > 0 ? request.Scopes : preset.DefaultScopes, _callers.Current?.Actor ?? "unknown", _time.GetUtcNow());
        if (request.Wait)
        {
            var finished = await started.Value.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
            var done = await Finish(sign, finished, cancellationToken).ConfigureAwait(false);
            return done.IsError ? done.Error : new AddConnectionResponse(ConnectionView.From(done.Value), ConsentUrl: null);
        }

        _ = FinishInBackground(sign, started.Value.Completion);
        return new AddConnectionResponse(ConnectionView.From(Pending(sign)), started.Value.ConsentUrl.ToString());
    }

    // why: nothing awaits this; a failed sign-in leaves no row and no secret, which is what a later 'connection list' shows
    private async Task FinishInBackground(SignInContext sign, Task<Result<TokenSet>> completion)
    {
        var finished = await completion.ConfigureAwait(false);
        _ = await Finish(sign, finished, CancellationToken.None).ConfigureAwait(false);
    }

    // The row is written only here, after the tokens are stored: a failed or denied sign-in never leaves a row or a secret.
    private async ValueTask<Result<Connection>> Finish(SignInContext sign, Result<TokenSet> tokens, CancellationToken cancellationToken)
    {
        if (tokens.IsError)
        {
            return ConnectionFailures.Safe(tokens.Error);
        }

        var pointer = _secrets.Store(sign.Name, tokens.Value);
        if (pointer.IsError)
        {
            return ConnectionFailures.Safe(pointer.Error);
        }

        var row = Pending(sign) with { SecretPointer = pointer.Value, Status = ConnectionStatus.Connected, StatusReason = null, UpdatedAt = _time.GetUtcNow() };
        var written = await _store.Upsert(row, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            _ = _secrets.Delete(pointer.Value);   // why: a secret with no row is one nobody can remove
            return ConnectionFailures.Safe(written.Error);
        }

        return row;
    }

    private static Connection Pending(SignInContext sign) => new(
        sign.Name,
        ConnectionKind.OAuth,
        sign.Preset.Key,
        new Dictionary<string, string>(StringComparer.Ordinal) { ["clientId"] = sign.Client, ["scopes"] = string.Join(' ', sign.Scopes) },
        SecretPointer: null,
        ConnectionStatus.NeedsSignIn,
        StatusReason: "waiting for the browser sign-in",
        sign.Actor,
        sign.Created,
        sign.Created,
        LastRefreshedAt: null);

    // An mcp connection takes --url and either --key-env (a key, stored like a rest one) or no key (the server's own OAuth sign-in through ISignInSession.StartMcp,
    // with a client registered on the spot). The row's config holds 'serverUrl'. Written in the green.
    private static ValueTask<Result<AddConnectionResponse>> AddMcp(ConnectionName name, AddConnectionRequest request, CancellationToken cancellationToken)
    {
        _ = (name, request, cancellationToken);
        throw new NotSupportedException();
    }

    private async ValueTask<Result<AddConnectionResponse>> AddKey(ConnectionName name, ConnectionKind kind, AddConnectionRequest request, ConnectionPreset? preset, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.KeyEnv))
        {
            return SindriErrors.InvalidInput.With($"a {ConnectionWire.Name(kind)} connection needs --key-env NAME (the environment variable that holds the key)");
        }

        var key = _environment.Get(request.KeyEnv);
        if (key is null)
        {
            return SindriErrors.InvalidInput.With($"the environment variable {request.KeyEnv} is not set or is empty (fix: export it in this shell, then run the command again)");
        }

        var pointer = _secrets.Store(name, new TokenSet(key, RefreshToken: null, ExpiresAt: null));
        if (pointer.IsError)
        {
            return ConnectionFailures.Safe(pointer.Error);
        }

        var now = _time.GetUtcNow();
        var config = new Dictionary<string, string>(StringComparer.Ordinal);
        if (preset?.ProbeUrl is { } probe)
        {
            config["probeUrl"] = probe;
        }

        var row = new Connection(name, kind, preset?.Key, config, pointer.Value, ConnectionStatus.Connected, StatusReason: null, _callers.Current?.Actor ?? "unknown", now, now, LastRefreshedAt: null);
        var written = await _store.Upsert(row, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            _ = _secrets.Delete(pointer.Value);
            return ConnectionFailures.Safe(written.Error);
        }

        return new AddConnectionResponse(ConnectionView.From(row), ConsentUrl: null);
    }

    private sealed record SignInContext(ConnectionName Name, ConnectionPreset Preset, string Client, IReadOnlyList<string> Scopes, string Actor, DateTimeOffset Created);
}
