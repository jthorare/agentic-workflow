using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features.AddConnection;

/// <summary>
/// Adds a connection. A bad name is SND-CONN-008, an existing one SND-CONN-010, a preset without a client id SND-CONN-007. An oauth connection starts the
/// sign-in; its row is written only after the tokens are stored, so a denied consent (SND-CONN-009), a timeout (SND-CONN-003) or a state mismatch
/// (SND-CONN-004) leaves no row and no secret. A rest or cli connection reads --key-env once and stores it as the secret. An error shows its code and
/// fixed title only (<see cref="ConnectionFailures"/>), and a log line names the connection and the code, never a credential.
/// </summary>
/// <param name="store">The connections.</param>
/// <param name="secrets">Stores the tokens.</param>
/// <param name="presets">The shipped presets.</param>
/// <param name="signIn">Starts the browser sign-in.</param>
/// <param name="environment">Reads --key-env.</param>
/// <param name="time">The clock.</param>
/// <param name="callers">Who is adding.</param>
/// <param name="logger">Where a failed background sign-in is reported (code only); null logs nothing.</param>
internal sealed partial class AddConnectionService(IConnectionStore store, IConnectionSecrets secrets, IConnectionPresetCatalog presets, ISignInSession signIn, IEnvironmentReader environment, TimeProvider time, ICallerContextAccessor callers, ILogger<AddConnectionService>? logger = null)
    : IService<AddConnectionRequest, AddConnectionResponse>
{
    private readonly IConnectionStore _store = store;
    private readonly IConnectionSecrets _secrets = secrets;
    private readonly IConnectionPresetCatalog _presets = presets;
    private readonly ISignInSession _signIn = signIn;
    private readonly IEnvironmentReader _environment = environment;
    private readonly TimeProvider _time = time;
    private readonly ICallerContextAccessor _callers = callers;
    private readonly ILogger? _logger = logger;

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
            _ => SindriErrors.InvalidInput.With("the mcp connection kind arrives in a later release (fix: use --kind rest or --kind cli with --key-env)"),
        };
    }

    private Result<ConnectionPreset?> Preset(string? key)
    {
        if (key is null)
        {
            return Results.Success<ConnectionPreset?>(null);
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

        var scopes = request.Scopes.Count > 0 ? request.Scopes : preset.DefaultScopes;
        var created = _time.GetUtcNow();
        var actor = _callers.Current?.Actor ?? "unknown";
        if (request.Wait)
        {
            var finished = await started.Value.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
            var done = await Finish(name, preset, request.Client, scopes, actor, created, finished, cancellationToken).ConfigureAwait(false);
            return done.IsError ? done.Error : new AddConnectionResponse(ConnectionView.From(done.Value), ConsentUrl: null);
        }

        _ = FinishInBackground(name, preset, request.Client, scopes, actor, created, started.Value.Completion);
        return new AddConnectionResponse(ConnectionView.From(Pending(name, preset, request.Client, scopes, actor, created)), started.Value.ConsentUrl.ToString());
    }

    private async Task FinishInBackground(ConnectionName name, ConnectionPreset preset, string client, IReadOnlyList<string> scopes, string actor, DateTimeOffset created, Task<Result<TokenSet>> completion)
    {
        var finished = await completion.ConfigureAwait(false);
        var done = await Finish(name, preset, client, scopes, actor, created, finished, CancellationToken.None).ConfigureAwait(false);
        if (done.IsError && _logger is not null)
        {
            LogSignInEnded(_logger, name.ToString(), done.Error.Code);
        }
    }

    // The row is written only here, after the tokens are stored: a failed or denied sign-in never leaves a row or a secret.
    private async ValueTask<Result<Connection>> Finish(ConnectionName name, ConnectionPreset preset, string client, IReadOnlyList<string> scopes, string actor, DateTimeOffset created, Result<TokenSet> tokens, CancellationToken cancellationToken)
    {
        if (tokens.IsError)
        {
            return ConnectionFailures.Safe(tokens.Error);
        }

        var pointer = _secrets.Store(name, tokens.Value);
        if (pointer.IsError)
        {
            return ConnectionFailures.Safe(pointer.Error);
        }

        var row = Pending(name, preset, client, scopes, actor, created) with { SecretPointer = pointer.Value, Status = ConnectionStatus.Connected, StatusReason = null, UpdatedAt = _time.GetUtcNow() };
        var written = await _store.Upsert(row, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            _ = _secrets.Delete(pointer.Value);   // why: a secret with no row is one nobody can remove
            return ConnectionFailures.Safe(written.Error);
        }

        return row;
    }

    private static Connection Pending(ConnectionName name, ConnectionPreset preset, string client, IReadOnlyList<string> scopes, string actor, DateTimeOffset created) => new(
        name,
        ConnectionKind.OAuth,
        preset.Key,
        new Dictionary<string, string>(StringComparer.Ordinal) { ["clientId"] = client, ["scopes"] = string.Join(' ', scopes) },
        SecretPointer: null,
        ConnectionStatus.NeedsSignIn,
        StatusReason: "waiting for the browser sign-in",
        actor,
        created,
        created,
        LastRefreshedAt: null);

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

    [LoggerMessage(Level = LogLevel.Warning, Message = "The sign-in for connection {Name} ended without a connection ({Code})")]
    private static partial void LogSignInEnded(ILogger logger, string name, string code);
}
