using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Aw.Context.Core.Linear;
using Aw.Context.Core.Sources;
using Aw.Kernel;
using Aw.Kernel.Json;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>
/// Sends Linear's five mutations with the write key (Linear hub spec 2026-11-04 §5.2, D2, D3). Nothing else holds a <see cref="LinearWriteKey"/>. One call is one 'POST' of the mutation to the profile's GraphQL url and nowhere else, under a 15 s deadline; the key goes only
/// into the Authorization header (a personal API key is sent as it is, without 'Bearer'), never an error or a log line. Failures are the hub's: no usable write key 002 (nothing sent), a refused key 004, a missing issue or comment 005, Linear unreachable before anything
/// was sent 010, sent and no answer (timeout, a reset connection, a 5xx) 011, a rate limit 012, and 'success: false' or any other GraphQL error 015 with Linear's own words. A body of the wrong shape is SND-CONTEXT-072.
/// </summary>
/// <param name="parts">The client, environment, clock and log every linear source shares.</param>
#pragma warning disable MA0182 // why: the Linear hub registration (DI, another change of the same PR) is its only user; the unit and fake-server tests build it directly
internal sealed class LinearWriteClient(LinearSourceParts parts) : ILinearWrites
{
    private const string WriteTokenKey = "context.sources.linear.writeToken";
    private const string RateLimitResetHeader = "x-ratelimit-requests-reset";
    private const string KeyFix = "Make a new one with write permission and paste it in Settings → Connections → Linear.";
    private const string Seconds = "15 s";
    private static readonly TimeSpan _deadline = TimeSpan.FromSeconds(15);
    private readonly ConcurrentDictionary<string, LinearWriteKey> _keys = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public async ValueTask<LinearWriteKeyStatus> KeyStatus(LinearSettings settings, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.WriteToken is not { } pointer)
        {
            return new LinearWriteKeyNotSet();
        }

        if (string.Equals(pointer, settings.Token, StringComparison.Ordinal))
        {
            return new LinearWriteKeySameAsReadKey();
        }

        if (IsLocalScheme(pointer) && !settings.Url.IsLoopback)
        {
            return new LinearWriteKeyRefused(
                $"{WriteTokenKey}: the write key must be in the keychain or 1Password, because an env var or file is readable by agent sessions. Paste it in Settings → Connections → Linear.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_deadline);
        try
        {
            var write = Usable(await new LinearWriteKey(pointer, parts.Environment).Get(timeout.Token).ConfigureAwait(false));
            if (write is null)
            {
                return new LinearWriteKeyRefused($"{WriteTokenKey}: the {Scheme(pointer)}: pointer doesn't resolve to a usable key (none, or one with spaces or control characters); 'sindri doctor' shows why.");
            }

            var read = await new LinearReadKey(settings.Token, parts.Environment).Get(timeout.Token).ConfigureAwait(false);
            return read is not null && Same(read, write) ? new LinearWriteKeySameAsReadKey() : new LinearWriteKeyResolves();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new LinearWriteKeyRefused(
                $"{WriteTokenKey}: the {Scheme(pointer)}: pointer didn't resolve within {Seconds} (a locked keychain, or op waiting for a sign-in?); 'sindri doctor' shows why.");
        }
    }

    /// <inheritdoc />
    public ValueTask<Result<LinearIssueView>> CreateIssue(LinearSettings settings, LinearIssueDraft draft, LinearClientId clientId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(clientId);
        return Send<LinearIssueWriteData>(settings, LinearWriteRequests.IssueCreate, LinearWriteRequests.CreateIssue(draft, clientId), static d => Of(d.IssueCreate), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<Result<LinearIssueView>> UpdateIssue(LinearSettings settings, ExternalId issueId, LinearIssueChange change, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(change);
        return Send<LinearIssueWriteData>(settings, LinearWriteRequests.IssueUpdate, LinearWriteRequests.UpdateIssue(issueId, change), static d => Of(d.IssueUpdate), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<Result<LinearIssueView>> CreateComment(LinearSettings settings, ExternalId issueId, string body, LinearClientId clientId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(clientId);
        return Send<LinearCommentWriteData>(settings, LinearWriteRequests.CommentCreate, LinearWriteRequests.CreateComment(issueId, body, clientId), static d => Of(d.CommentCreate), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<Result<LinearIssueView>> UpdateComment(LinearSettings settings, ExternalId commentId, string body, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(body);
        return Send<LinearCommentWriteData>(settings, LinearWriteRequests.CommentUpdate, LinearWriteRequests.UpdateComment(commentId, body), static d => Of(d.CommentUpdate), cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<Result<LinearIssueView>> LinkUrl(LinearSettings settings, ExternalId issueId, Uri url, string title, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(title);
        return Send<LinearAttachmentWriteData>(settings, LinearWriteRequests.AttachmentLinkUrl, LinearWriteRequests.LinkUrl(issueId, url, title), static d => Of(d.AttachmentLinkUrl), cancellationToken);
    }

    private sealed record Outgoing(LinearCalls Calls, Uri Url, string Operation, LinearRequest Request);

    private static (bool Success, LinearIssue? Issue)? Of(LinearIssueWritePayload? payload) => payload is null ? null : (payload.Success, payload.Issue);

    private static (bool Success, LinearIssue? Issue)? Of(LinearCommentWritePayload? payload) => payload is null ? null : (payload.Success, payload.Comment?.Issue);

    private static (bool Success, LinearIssue? Issue)? Of(LinearAttachmentWritePayload? payload) => payload is null ? null : (payload.Success, payload.Attachment?.Issue);

    private static string? Usable(string? key) => string.IsNullOrEmpty(key) || key.Any(static c => char.IsControl(c) || char.IsWhiteSpace(c)) ? null : key;

    private static bool IsLocalScheme(string pointer) => Scheme(pointer) is var scheme && (string.Equals(scheme, "env", StringComparison.Ordinal) || string.Equals(scheme, "file", StringComparison.Ordinal));

    private static bool Same(string read, string write) => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(read), Encoding.UTF8.GetBytes(write));

    private static string Scheme(string pointer) => pointer[..pointer.IndexOf(':', StringComparison.Ordinal)];

    // Connection refused, no such host, a failed TLS handshake: nothing reached Linear. Any other transport failure may have come after the request left.
    private static bool NothingSent(HttpRequestError error) =>
        error is HttpRequestError.ConnectionError or HttpRequestError.NameResolutionError or HttpRequestError.SecureConnectionError or HttpRequestError.ProxyTunnelError;

    private async ValueTask<Result<LinearIssueView>> Send<TData>(
        LinearSettings settings, string operation, LinearRequest request, Func<TData, (bool Success, LinearIssue? Issue)?> pick, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settings);
        cancellationToken.ThrowIfCancellationRequested();
        var calls = new LinearCalls(settings, parts.Logger, KeyFix, LinearErrorSet.Write);
        if (settings.WriteToken is not { } pointer)
        {
            return calls.Failed(
                operation, LinearHubErrors.NoWriteKey.With("Edits need write access. Paste a write key in Settings → Connections → Linear (or run `sindri index connect linear --write`)."), status: null);
        }

        var key = _keys.GetOrAdd(pointer, static (p, environment) => new LinearWriteKey(p, environment), parts.Environment);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_deadline);
        try
        {
            var secret = Usable(await key.Get(timeout.Token).ConfigureAwait(false));
            if (secret is null)
            {
                return calls.Failed(operation, Unresolved(pointer), status: null);
            }

            var read = await Post<TData>(new Outgoing(calls, settings.Url, operation, request), key, secret, timeout.Token).ConfigureAwait(false);
            return read.IsError ? read.Error : View(calls, operation, settings, pick(read.Value));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return calls.Failed(operation, key.Settled ? NoAnswer(settings, operation, "didn't answer it within " + Seconds) : Unresolved(pointer, "didn't resolve within " + Seconds + " (a locked keychain, or op waiting for a sign-in?)"), status: null);
        }
        catch (HttpRequestException e)
        {
            return calls.Failed(operation, NothingSent(e.HttpRequestError) ? Unreachable(settings, e.HttpRequestError) : NoAnswer(settings, operation, $"lost the connection ({e.HttpRequestError})"), status: null);
        }
    }

    private async Task<Result<TData>> Post<TData>(Outgoing call, LinearWriteKey key, string secret, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, call.Url) { Content = new ByteArrayContent(AwJson.Serialize(call.Request, LinearWire.Options)) };
        message.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        _ = message.Headers.TryAddWithoutValidation("Authorization", secret);
        using var response = await parts.Http.SendAsync(message, cancellationToken).ConfigureAwait(false);
        var reply = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        var reset = response.Headers.TryGetValues(RateLimitResetHeader, out var values) ? values.FirstOrDefault() : null;
        var read = call.Calls.Read<TData>(call.Operation, response.StatusCode, reply, reset);
        if (read.IsError && string.Equals(read.Error.Code, LinearHubErrors.WriteKeyRefused.Code, StringComparison.Ordinal))
        {
            key.Forget();
        }

        return read;
    }

    private static Error Unresolved(string pointer, string? why = null) => LinearHubErrors.NoWriteKey.With(
        $"{WriteTokenKey}: the {Scheme(pointer)}: pointer {why ?? "doesn't resolve to a usable Linear write key (none, or one with spaces or control characters)"}; 'sindri doctor' shows why. Edits need write access.");

    private static Error NoAnswer(LinearSettings settings, string operation, string what) => LinearHubErrors.NoAnswer.With($"Linear at {settings.Address} {what}, so {operation} may or may not have been applied.");

    private static Error Unreachable(LinearSettings settings, HttpRequestError error) => LinearHubErrors.Unreachable.With($"Couldn't reach Linear at {settings.Address} ({error}), so nothing was changed. Try again.");

    private static Result<LinearIssueView> View(LinearCalls calls, string operation, LinearSettings settings, (bool Success, LinearIssue? Issue)? written)
    {
        if (written is not { } payload || (payload.Success && payload.Issue is null))
        {
            return calls.Failed(operation, calls.Unexpected($"answered {operation} without the mutation's payload"), status: null);
        }

        if (!payload.Success)
        {
            return calls.Failed(operation, LinearWriteErrors.Refusal($"Linear at {settings.Address} didn't apply it (success: false)"), status: null);
        }

        return payload.Issue is { } issue && LinearIssueViews.Of(issue) is { } view
            ? view
            : calls.Failed(operation, calls.Unexpected($"answered {operation} with an issue the adapter can't read"), status: null);
    }
}

#pragma warning restore MA0182
