using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Connections;

namespace Aw.Sindri.Features.AddConnection;

/// <summary>
/// Signs a stored connection in again without removing it: the same loopback sign-in as add, started from the row's own preset, client id, scopes (or MCP server address)
/// and the client secret kept beside its tokens. The row stays (creator, creation time, name) and only its tokens are replaced once the callback lands; a failed sign-in
/// leaves the row and its old secret as they were. A key-based connection (rest, cli) has no browser sign-in: SND-CONN-011 says to remove it and add it with a new key.
/// An unknown name is SND-CONN-001.
/// </summary>
/// <param name="store">The connections.</param>
/// <param name="secrets">Reads the client secret stored beside the tokens.</param>
/// <param name="presets">The shipped presets.</param>
/// <param name="callers">Who is signing in.</param>
/// <param name="oauth">The oauth sign-in.</param>
/// <param name="events">Records who signed in (#2460).</param>
internal sealed class SignInConnectionService(IConnectionStore store, IConnectionSecrets secrets, IConnectionPresetCatalog presets, ICallerContextAccessor callers, OAuthSignInFlow oauth, ConnectionEvents events)
    : IService<SignInConnectionRequest, AddConnectionResponse>
{
    private readonly IConnectionStore _store = store;
    private readonly IConnectionSecrets _secrets = secrets;
    private readonly IConnectionPresetCatalog _presets = presets;
    private readonly ICallerContextAccessor _callers = callers;
    private readonly OAuthSignInFlow _oauth = oauth;
    private readonly ConnectionEvents _events = events;

    public async ValueTask<Result<AddConnectionResponse>> Handle(SignInConnectionRequest request, CancellationToken cancellationToken)
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

        _ = _events;   // red stub (#2460): the event is not appended yet
        var row = found.Value;
        var actor = _callers.Current?.Actor ?? "unknown";
        if (row.Kind == ConnectionKind.OAuth)
        {
            return await SignInOAuth(row, actor, request.Wait, cancellationToken).ConfigureAwait(false);
        }

        if (row.Kind != ConnectionKind.Mcp)
        {
            return NoSignIn(row);
        }

        return HasServer(row)
            ? await SignInMcp(row, actor, request.Wait, cancellationToken).ConfigureAwait(false)
            : SindriErrors.InvalidInput.With($"{row.Name} has no server address stored to sign in with (fix: sindri connection remove {row.Name}, then add it again with --url)");
    }

    private static bool HasServer(Connection row) => row.Config.TryGetValue("serverUrl", out var url) && Uri.TryCreate(url, UriKind.Absolute, out _);

    private static Error NoSignIn(Connection row)
        => SindriErrors.ConnectionKindNoSignIn.With($"{row.Name} uses a key, not a browser sign-in (fix: sindri connection remove {row.Name}, then sindri connection add {row.Name} --key-env NAME with a new key)");

    private async ValueTask<Result<AddConnectionResponse>> SignInOAuth(Connection row, string actor, bool wait, CancellationToken cancellationToken)
    {
        var preset = row.Preset is null ? null : _presets.Find(row.Preset);
        if (preset is null || !row.Config.TryGetValue("clientId", out var client) || string.IsNullOrWhiteSpace(client))
        {
            return SindriErrors.InvalidInput.With($"{row.Name} has no preset or client id stored to sign in with (fix: sindri connection remove {row.Name}, then add it again)");
        }

        if (!preset.LoopbackAllowed)
        {
            return SindriErrors.InvalidInput.With($"{preset.DisplayName} does not accept the browser sign-in Sindri runs on this machine. {preset.SignInNote}");
        }

        var scopes = row.Config.TryGetValue("scopes", out var text) ? text.Split(' ', StringSplitOptions.RemoveEmptyEntries) : [];
        var clientSecret = row.SecretPointer is { } pointer && _secrets.Read(pointer) is { IsSuccess: true } stored ? stored.Value.ClientSecret : null;
        return await _oauth.Run(new OAuthAdd(row.Name, preset, client, clientSecret, scopes, actor, wait, row), cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<AddConnectionResponse>> SignInMcp(Connection row, string actor, bool wait, CancellationToken cancellationToken)
        => await _oauth.RunMcp(new McpAdd(row.Name, new Uri(row.Config["serverUrl"]), actor, wait, row), cancellationToken).ConfigureAwait(false);
}
