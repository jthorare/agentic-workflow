using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Json;
using Aw.Sindri.Core;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// Where T3 Code's MCP endpoint is and the bearer token it takes. The endpoint comes from T3's own runtime file, so it follows the port T3 picked.
/// The token is an MCP-client session minted with the 't3' CLI, held in memory only (never on disk), renewed before it expires and revoked when
/// Sindri stops.
/// </summary>
/// <remarks>
/// A plain bearer session with the 'mcp-client' subject is what T3's /mcp accepts from an outside process; 'mcp-client' is T3's internal subject name,
/// so this is the fragile part of the adapter. Once 't3 thread start --worktree-path' and 't3 thread interrupt' exist
/// (proposed in pingdotgg/t3code discussion #23945), the host can call the 't3 thread' CLI instead, which signs itself in, and this class goes away.
/// </remarks>
/// <param name="options">The T3 home and CLI.</param>
/// <param name="time">The clock the token's expiry is read on.</param>
internal sealed class T3Credentials(T3Options options, TimeProvider time) : IAsyncDisposable, IDisposable
{
    private static readonly JsonSerializerOptions _json = AwJson.CreateOptions(T3JsonContext.Default);
    private static readonly TimeSpan _renewBefore = TimeSpan.FromMinutes(5);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private T3Wire.IssuedSession? _session;

    /// <summary>The running server's MCP endpoint, read from T3's runtime file each time (T3 rewrites it when it restarts on another port).</summary>
    /// <returns>The endpoint, or SND-LAUNCH-025 when no T3 server is running.</returns>
    public Result<Uri> Endpoint()
    {
        var path = Path.Combine(options.T3Home, "userdata", "server-runtime.json");
        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return SindriErrors.LaunchT3Unavailable.With($"no running T3 Code server: {path} can't be read (fix: open T3 Code)");
        }

        var state = AwJson.Deserialize<T3Wire.RuntimeState>(bytes, _json);
        return state.IsSuccess && Uri.TryCreate(state.Value.Origin, UriKind.Absolute, out var origin)
            ? new Uri(origin, "/mcp")
            : SindriErrors.LaunchT3Unavailable.With($"{path} names no server origin (fix: restart T3 Code)");
    }

    /// <summary>A token that is good for at least five more minutes, minting one when needed.</summary>
    /// <param name="cancellationToken">Cancels the wait.</param>
    /// <returns>The token, or SND-LAUNCH-025 when the 't3' CLI can't mint one.</returns>
    public async ValueTask<Result<string>> Token(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_session is { } held && held.ExpiresAt - _renewBefore > time.GetUtcNow())
            {
                return held.Token;
            }

            var issued = await Issue(cancellationToken).ConfigureAwait(false);
            if (issued.IsError)
            {
                return issued.Error;
            }

            await Revoke(_session).ConfigureAwait(false);   // the expiring one is replaced, not left to time out
            _session = issued.Value;
            return issued.Value.Token;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <summary>Drops the held token (T3 refused it), so the next call mints a new one.</summary>
    public void Drop() => _session = null;

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await Revoke(_session).ConfigureAwait(false);
        _session = null;
        _gate.Dispose();
    }

    /// <inheritdoc/>
    /// <remarks>A container disposed synchronously can't wait for the revoke: it is started and left to finish, and the token expires within the hour either way.</remarks>
    public void Dispose()
    {
        _ = Revoke(_session).AsTask();
        _session = null;
        _gate.Dispose();
    }

    private async ValueTask<Result<T3Wire.IssuedSession>> Issue(CancellationToken cancellationToken)
    {
        string[] arguments =
        [
            "auth", "session", "issue", "--subject", "mcp-client", "--scope", "orchestration:read", "--scope", "orchestration:operate",
            "--label", "sindri", "--ttl", "1h", "--json",
        ];
        var output = await ViewerProcess.Output(options.Cli, arguments, options.CallBudget, cancellationToken).ConfigureAwait(false);
        if (output is null)
        {
            return SindriErrors.LaunchT3Unavailable.With($"'{options.Cli} auth session issue' failed (fix: install T3 Code's t3 command, or set T3CODE_CLI_PATH)");
        }

        var issued = AwJson.Deserialize<T3Wire.IssuedSession>(System.Text.Encoding.UTF8.GetBytes(output), _json);
        return issued.IsSuccess ? issued.Value : SindriErrors.LaunchT3Unavailable.With("'t3 auth session issue --json' printed something else than a session");
    }

    private async ValueTask Revoke(T3Wire.IssuedSession? session)
    {
        if (session is not null)
        {
            _ = await ViewerProcess.Output(options.Cli, ["auth", "session", "revoke", session.SessionId], options.CallBudget, CancellationToken.None).ConfigureAwait(false);   // best effort: an unrevoked one expires within the hour
        }
    }
}
