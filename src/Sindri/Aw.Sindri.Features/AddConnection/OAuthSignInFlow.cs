using Aw.Kernel;
using Aw.Sindri.Core.Connections;
using Aw.Sindri.Core.Connections.OAuth;
using Microsoft.Extensions.Logging;

namespace Aw.Sindri.Features.AddConnection;

/// <summary>
/// The oauth half of AddConnection: starts the sign-in, and writes the row only after the tokens are stored, so a denied consent (SND-CONN-009), a timeout
/// (SND-CONN-003) or a state mismatch (SND-CONN-004) leaves no row and no secret. A request that waits ties the sign-in to its own token; a request that does not
/// (an agent, which gets the consent URL at once) runs the sign-in under its own 5-minute limit alone, so the request ending cannot cancel the owner's page.
/// A failed sign-in is remembered in <see cref="SignInOutcomes"/> and logged by code.
/// </summary>
/// <param name="store">The connections.</param>
/// <param name="secrets">Stores the tokens.</param>
/// <param name="signIn">Starts the browser sign-in.</param>
/// <param name="time">The clock.</param>
/// <param name="outcomes">Remembers a failed sign-in.</param>
/// <param name="events">Records that a connection is signed in again, so its Needs-you row clears.</param>
/// <param name="logger">Logs a failed sign-in by code; null logs nothing.</param>
internal sealed partial class OAuthSignInFlow(IConnectionStore store, IConnectionSecrets secrets, ISignInSession signIn, TimeProvider time, SignInOutcomes outcomes, ConnectionEvents events, ILogger<OAuthSignInFlow>? logger = null)
{
    internal async ValueTask<Result<AddConnectionResponse>> Run(OAuthAdd add, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(add);
        outcomes.Clear(add.Name.ToString());
        var started = await signIn.Start(add.Preset, add.Client, add.Scopes, add.Secret, Limit(add.Wait, cancellationToken)).ConfigureAwait(false);
        return started.IsError
            ? ConnectionFailures.Safe(started.Error)
            : await Follow(Sign.Of(add, time.GetUtcNow()), started.Value, add.Wait, cancellationToken).ConfigureAwait(false);
    }

    // An mcp server's own sign-in: its metadata, a client registered on the spot, then the same loopback callback. The start's failure text is fixed text with no server content, so it is kept (it says how to use a key instead).
    internal async ValueTask<Result<AddConnectionResponse>> RunMcp(McpAdd add, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(add);
        outcomes.Clear(add.Name.ToString());
        var started = await signIn.StartMcp(add.Server, Limit(add.Wait, cancellationToken)).ConfigureAwait(false);
        return started.IsError
            ? started.Error
            : await Follow(Sign.Of(add, time.GetUtcNow()), started.Value, add.Wait, cancellationToken).ConfigureAwait(false);
    }

    // A request that waits ties the sign-in to its own token; one that does not (an agent) runs it under its own 5-minute limit alone.
    private static CancellationToken Limit(bool wait, CancellationToken cancellationToken) => wait ? cancellationToken : CancellationToken.None;

    private async ValueTask<Result<AddConnectionResponse>> Follow(Sign sign, SignInStarted started, bool wait, CancellationToken cancellationToken)
    {
        if (wait)
        {
            var finished = await started.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
            var done = await Finish(sign, finished, cancellationToken).ConfigureAwait(false);
            return done.IsError ? done.Error : new AddConnectionResponse(ConnectionView.From(done.Value), ConsentUrl: null);
        }

        _ = FinishInBackground(sign, started.Completion);
        return new AddConnectionResponse(ConnectionView.From(sign.Pending), started.ConsentUrl.ToString());
    }

    // why: nothing awaits this; the outcome of a failed sign-in is kept in memory (GET) and logged by code
    private async Task FinishInBackground(Sign sign, Task<Result<TokenSet>> completion)
    {
        var finished = await completion.ConfigureAwait(false);
        _ = await Finish(sign, finished, CancellationToken.None).ConfigureAwait(false);
    }

    private async ValueTask<Result<Connection>> Finish(Sign sign, Result<TokenSet> tokens, CancellationToken cancellationToken)
    {
        var done = await Store(sign, tokens, cancellationToken).ConfigureAwait(false);
        var name = sign.Pending.Name.ToString();
        if (done.IsError)
        {
            outcomes.Failed(name, done.Error);
            if (logger is not null)
            {
                LogFailed(logger, name, done.Error.Code);
            }
        }
        else
        {
            outcomes.Clear(name);
            await events.SignedIn(sign.Pending.Name, cancellationToken).ConfigureAwait(false);   // a stale 'sign in again' row for this name goes away
        }

        return done;
    }

    // The row is written only here, after the tokens are stored: a failed or denied sign-in never leaves a row or a secret.
    private async ValueTask<Result<Connection>> Store(Sign sign, Result<TokenSet> tokens, CancellationToken cancellationToken)
    {
        if (tokens.IsError)
        {
            return ConnectionFailures.Safe(tokens.Error);
        }

        var pointer = secrets.Store(sign.Pending.Name, tokens.Value with { ClientSecret = sign.ClientSecret });
        if (pointer.IsError)
        {
            return ConnectionFailures.Safe(pointer.Error);
        }

        var now = time.GetUtcNow();
        var row = sign.Pending with { SecretPointer = pointer.Value, Status = ConnectionStatus.Connected, StatusReason = null, UpdatedAt = now, LastRefreshedAt = sign.Replaces ? now : sign.Pending.LastRefreshedAt };
        var written = sign.Replaces
            ? await store.Update(row, cancellationToken).ConfigureAwait(false)
            : await store.Insert(row, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            if (!sign.Replaces)
            {
                _ = secrets.Delete(pointer.Value);   // why: a secret with no row is one nobody can remove (a row that already existed keeps its pointer)
            }

            return ConnectionFailures.Safe(written.Error);
        }

        return row;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The sign-in for connection {Name} ended without a connection ({Code})")]
    private static partial void LogFailed(ILogger logger, string name, string code);

    /// <summary>One sign-in in flight: the row it will become (waiting for the browser) and the client secret to store with its tokens.</summary>
    private sealed record Sign(Connection Pending, string? ClientSecret, bool Replaces = false)
    {
        internal static Sign Of(OAuthAdd add, DateTimeOffset created) => add.Existing is { } kept
            ? new(Waiting(kept), add.Secret, Replaces: true)
            : new(
            new Connection(
                add.Name,
                ConnectionKind.OAuth,
                add.Preset.Key,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["clientId"] = add.Client, ["scopes"] = string.Join(' ', add.Scopes.Count > 0 ? add.Scopes : add.Preset.DefaultScopes) },
                SecretPointer: null,
                ConnectionStatus.NeedsSignIn,
                StatusReason: "waiting for the browser sign-in",
                add.Actor,
                created,
                created,
                LastRefreshedAt: null),
            add.Secret);

        internal static Sign Of(McpAdd add, DateTimeOffset created) => add.Existing is { } kept
            ? new(Waiting(kept), ClientSecret: null, Replaces: true)
            : new(
            new Connection(
                add.Name,
                ConnectionKind.Mcp,
                Preset: null,
                new Dictionary<string, string>(StringComparer.Ordinal) { ["serverUrl"] = add.Server.ToString() },
                SecretPointer: null,
                ConnectionStatus.NeedsSignIn,
                StatusReason: "waiting for the browser sign-in",
                add.Actor,
                created,
                created,
                LastRefreshedAt: null),
            ClientSecret: null);

        // The stored row stays as it is until the callback lands; only the text the caller sees says it is waiting.
        private static Connection Waiting(Connection kept) => kept with { StatusReason = "waiting for the browser sign-in" };
    }
}
