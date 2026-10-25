using System.Globalization;
using System.Net;
using Aw.Context.Core;
using Aw.Context.Core.Onyx;
using Aw.Kernel;
using Aw.Kernel.Json;
using Microsoft.Extensions.Logging;

namespace Aw.Context.Infrastructure.Onyx;

/// <summary>
/// Sends the onyx adapter's calls with the key and reads Onyx v4.9.0's replies into typed records. The key comes from
/// calling the environment with the settings' pointer text (Sindri resolves pointers, spec §4), once per client: a
/// client belongs to one source, which serves one query, so a keychain: or op: pointer starts one process per query,
/// not one per request, and a pointer that didn't resolve is tried again by the next query. The resolution runs off the
/// calling thread and is awaited under the call's deadline and the caller's cancellation, so a locked keychain or an op
/// sign-in makes the call 010 on time (the tool itself ends at its own timeout in the background). The key goes only into
/// the Authorization header, never into an error or a log line. Failures map to the catalogue: no usable key 002, no
/// answer in time or a 5xx 010, a 401 or 403 050, a 404 on a document route 052, any other status or shape 051.
/// </summary>
/// <param name="settings">The checked settings.</param>
/// <param name="parts">The client, environment, clock and log every onyx source shares.</param>
internal sealed partial class OnyxClient(OnyxSettings settings, OnyxSourceParts parts)
{
    private const string TokenKey = "context.sources.onyx.token";
    private readonly OnyxSourceParts _parts = parts;
    private readonly Lazy<Task<string?>> _key = new(() => Task.Run(() => parts.Environment(settings.Token)));

    /// <summary>The checked settings.</summary>
    public OnyxSettings Settings { get; } = settings;

    /// <summary>The HTTP client.</summary>
    public HttpClient Http => _parts.Http;

    /// <summary>Sends one call with the key and reads the reply.</summary>
    /// <param name="call">The call (allowlisted by <see cref="OnyxRequests"/>).</param>
    /// <param name="deadline">How long Onyx gets to answer.</param>
    /// <param name="cancellationToken">The caller's cancellation, which is thrown, not turned into an error.</param>
    /// <typeparam name="T">The reply record.</typeparam>
    /// <returns>The reply, or an error from the catalogue.</returns>
    public async ValueTask<Result<T>> Call<T>(OnyxCall call, TimeSpan deadline, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(call);
        cancellationToken.ThrowIfCancellationRequested();   // before the spent-deadline answer: a cancelled caller gets the thrown cancellation
        if (deadline <= TimeSpan.Zero)
        {
            return Failed(call, Unavailable($"wasn't called: the query's deadline was spent before {call.Route}"), status: null);   // never a race, never an ArgumentOutOfRangeException
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(deadline);
        try
        {
            var key = await Key(timeout.Token).ConfigureAwait(false);
            if (string.IsNullOrEmpty(key) || key.Any(c => char.IsControl(c) || char.IsWhiteSpace(c)))
            {
                return Failed(call, ContextErrors.SourceNotConfigured.With(
                    $"{TokenKey}: the {Scheme(Settings.Token)}: secret pointer doesn't resolve to a usable Onyx key (none, or one with spaces or "
                    + "control characters); 'sindri doctor' shows why (its SND-SECRET code names the target and the fix). "
                    + "Point it at a dedicated service account's admin key (spec §8.8)."), status: null);
            }

            var reply = await OnyxRequests.Send(_parts.Http, Settings.Url, call, key, timeout.Token).ConfigureAwait(false);
            return Read<T>(call, reply);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            var what = _key.Value.IsCompleted
                ? $"didn't answer {call.Route}"
                : $"wasn't called: the {TokenKey} pointer didn't resolve (a locked keychain, or op waiting for a sign-in?)";
            return Failed(call, Unavailable(string.Create(CultureInfo.InvariantCulture,
                $"{what} within {deadline.TotalSeconds:0.###} s")), status: null);
        }
        catch (HttpRequestException e)
        {
            return Failed(call, Unavailable($"isn't reachable ({e.HttpRequestError}); start Onyx or check context.sources.onyx.url"), status: null);
        }
    }

    /// <summary>The resolved key, or null when the resolution failed; a resolution that throws counts as not resolved (002), never as an exception.</summary>
    private async Task<string?> Key(CancellationToken cancellationToken)
    {
        try
        {
            return await _key.Value.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            return null;   // Sindri's doctor names the reason; the exception's text may name a path and stays out of the answer
        }
    }

    /// <summary>The pointer's scheme ('env', 'file', 'keychain', 'op'; OnyxSettings checked the form): never its target, which Sindri's doctor names.</summary>
    private static string Scheme(string pointer) => pointer[..pointer.IndexOf(':', StringComparison.Ordinal)];

    private Result<T> Read<T>(OnyxCall call, OnyxReply reply)
    {
        var status = (int)reply.Status;
        if (status is >= 200 and < 300)
        {
            var value = AwJson.Deserialize<T>(reply.Body, OnyxWire.Options);
            return value.IsSuccess ? value : Failed(call, Unexpected($"answered {call.Route} with a body"), status);
        }

        return Failed(call, StatusError(call, reply.Status), status);
    }

    private Error StatusError(OnyxCall call, HttpStatusCode status)
    {
        var answered = string.Create(CultureInfo.InvariantCulture, $"answered HTTP {(int)status} on {call.Route}");
        if (status is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
        {
            var why = call.Route == "/api/me"
                ? "the key isn't accepted (missing, wrong, expired or revoked; /api/me needs only a valid key)"
                : "the key is missing, wrong, or lacks the admin role POST /api/admin/search needs";
            return ContextErrors.OnyxKeyRefused.With(
                $"Onyx at {Settings.Address} refused the key on {call.Method} {call.Route} (HTTP {((int)status).ToString(CultureInfo.InvariantCulture)}): {why}. Fix: create an admin key for a dedicated account "
                + $"(`sindri onyx up` makes one), store it behind {TokenKey} (env:, file:, keychain: or op:), then approve the profile again (`sindri profile approve`). "
                + $"If this isn't your Onyx, another service may answer on {Settings.Address}: check `curl {Settings.Url}api/version` shows a backend_version (spec §8.8).");
        }

        if (status == HttpStatusCode.NotFound && call.Route.StartsWith("/api/document/", StringComparison.Ordinal))
        {
            return ContextErrors.OnyxDocumentNotFound.With($"Onyx at {Settings.Address} has no such document ({call.Route} answered 404).");
        }

        return (int)status >= 500 ? Unavailable(answered) : Unexpected(answered);
    }

    private Error Unavailable(string what) => ContextErrors.SourceUnavailable.With($"Onyx at {Settings.Address} {what}.");

    private Error Unexpected(string what) => ContextErrors.OnyxUnexpectedReply.With(
        $"Onyx at {Settings.Address} {what} that the pinned {Settings.Version} doesn't send; check the running release against context.sources.onyx.version (spec §8.8).");

    private Error Failed(OnyxCall call, Error error, int? status)
    {
        LogFailed(_parts.Logger, call.Method.Method, call.Route, status, error.Code, error.Message);
        return error;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Onyx call {Method} {Route} failed (HTTP {Status}): {Code} {Reason}")]
    private static partial void LogFailed(ILogger logger, string method, string route, int? status, string code, string reason);
}
