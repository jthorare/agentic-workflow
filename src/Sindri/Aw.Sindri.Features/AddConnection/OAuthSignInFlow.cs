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
/// <param name="logger">Logs a failed sign-in by code; null logs nothing.</param>
internal sealed partial class OAuthSignInFlow(IConnectionStore store, IConnectionSecrets secrets, ISignInSession signIn, TimeProvider time, SignInOutcomes outcomes, ILogger<OAuthSignInFlow>? logger = null)
{
    internal async ValueTask<Result<AddConnectionResponse>> Run(OAuthAdd add, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(add);
        var name = add.Name.ToString();
        outcomes.Clear(name);
        var token = add.Wait ? cancellationToken : CancellationToken.None;
        var started = await signIn.Start(add.Preset, add.Client, add.Scopes, add.Secret, token).ConfigureAwait(false);
        if (started.IsError)
        {
            return ConnectionFailures.Safe(started.Error);
        }

        var sign = new Sign(add, add.Scopes.Count > 0 ? add.Scopes : add.Preset.DefaultScopes, time.GetUtcNow());
        if (add.Wait)
        {
            var finished = await started.Value.Completion.WaitAsync(cancellationToken).ConfigureAwait(false);
            var done = await Finish(sign, finished, cancellationToken).ConfigureAwait(false);
            return done.IsError ? done.Error : new AddConnectionResponse(ConnectionView.From(done.Value), ConsentUrl: null);
        }

        _ = FinishInBackground(sign, started.Value.Completion);
        return new AddConnectionResponse(ConnectionView.From(Pending(sign)), started.Value.ConsentUrl.ToString());
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
        var name = sign.Add.Name.ToString();
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

        var pointer = secrets.Store(sign.Add.Name, tokens.Value with { ClientSecret = sign.Add.Secret });
        if (pointer.IsError)
        {
            return ConnectionFailures.Safe(pointer.Error);
        }

        var row = Pending(sign) with { SecretPointer = pointer.Value, Status = ConnectionStatus.Connected, StatusReason = null, UpdatedAt = time.GetUtcNow() };
        var written = await store.Insert(row, cancellationToken).ConfigureAwait(false);
        if (written.IsError)
        {
            _ = secrets.Delete(pointer.Value);   // why: a secret with no row is one nobody can remove
            return ConnectionFailures.Safe(written.Error);
        }

        return row;
    }

    private static Connection Pending(Sign sign) => new(
        sign.Add.Name,
        ConnectionKind.OAuth,
        sign.Add.Preset.Key,
        new Dictionary<string, string>(StringComparer.Ordinal) { ["clientId"] = sign.Add.Client, ["scopes"] = string.Join(' ', sign.Scopes) },
        SecretPointer: null,
        ConnectionStatus.NeedsSignIn,
        StatusReason: "waiting for the browser sign-in",
        sign.Add.Actor,
        sign.Created,
        sign.Created,
        LastRefreshedAt: null);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The sign-in for connection {Name} ended without a connection ({Code})")]
    private static partial void LogFailed(ILogger logger, string name, string code);

    private sealed record Sign(OAuthAdd Add, IReadOnlyList<string> Scopes, DateTimeOffset Created);
}
