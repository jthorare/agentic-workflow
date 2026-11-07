using System.Globalization;
using System.Net;
using Aw.Context.Core;
using Aw.Context.Core.Linear;
using Aw.Kernel;
using Aw.Kernel.Json;
using Microsoft.Extensions.Logging;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>
/// What every call to Linear shares, whichever key sends it (Linear hub spec 2026-11-04 §5.2): reading a reply into the data or the catalogue's error, classifying GraphQL errors, naming Linear's address in a message, and logging a failed
/// operation by its name, status and code. The read client and the write client use it. The advice for a refused key differs by key, so the caller gives it, and so does the catalogue the errors come from (<see cref="LinearErrorSet"/>; the read key's by default).
/// </summary>
/// <param name="settings">The checked settings.</param>
/// <param name="logger">The log; it gets operations, statuses and codes, never a key or a body.</param>
/// <param name="keyFix">What to do about a refused key, as a sentence (the read key's is to make a read-only key).</param>
/// <param name="errorSet">Which catalogue the failures come from; null for the read key's.</param>
internal sealed partial class LinearCalls(LinearSettings settings, ILogger logger, string keyFix, LinearErrorSet? errorSet = null)
{
    private readonly LinearErrorSet _errors = errorSet ?? LinearErrorSet.Read;

    /// <summary>Reads Linear's reply to one operation: the data, or the catalogue's error for a refused key, a rate limit, a 5xx, a body that isn't the shape the operation expects or GraphQL errors.</summary>
    /// <param name="operation">The operation's name, for the messages and the log.</param>
    /// <param name="status">The HTTP status.</param>
    /// <param name="body">The reply's bytes.</param>
    /// <param name="rateLimitReset">The reply's 'x-ratelimit-requests-reset' header, when it had one.</param>
    /// <typeparam name="T">The data record of the operation.</typeparam>
    /// <returns>The data or the error.</returns>
    public Result<T> Read<T>(string operation, HttpStatusCode status, byte[] body, string? rateLimitReset = null)
    {
        var code = (int)status;
        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            return Failed(operation, KeyRefused(), code);
        }

        if (status == HttpStatusCode.TooManyRequests)
        {
            return Failed(operation, _errors.RateLimited(settings.Address, rateLimitReset), code);
        }

        if (code >= 500)
        {
            return Failed(operation, _errors.ServerFailed(settings.Address, operation, code), code);
        }

        var reply = AwJson.Deserialize<LinearReply<T>>(body, LinearWire.Options);
        if (reply.IsError)
        {
            return Failed(operation, Unexpected(string.Create(CultureInfo.InvariantCulture, $"answered {operation} (HTTP {code}) with a body")), code);
        }

        if (reply.Value.Errors is { Count: > 0 } errors)
        {
            return Failed(operation, Classify(errors, rateLimitReset), code);
        }

        return reply.Value.Data is { } data && code is >= 200 and < 300
            ? data
            : Failed(operation, Unexpected(string.Create(CultureInfo.InvariantCulture, $"answered {operation} with HTTP {code} and no data")), code);
    }

    private Error Classify(IReadOnlyList<LinearGraphQlError> graphQlErrors, string? rateLimitReset)
    {
        if (graphQlErrors.Any(e => IsAuthCode(e.Extensions?.Code)))
        {
            return KeyRefused();
        }

        if (graphQlErrors.Any(static e => string.Equals(e.Extensions?.Code, "RATELIMITED", StringComparison.Ordinal)))
        {
            return _errors.RateLimited(settings.Address, rateLimitReset);
        }

        return graphQlErrors.All(e => e.Message.StartsWith("Entity not found", StringComparison.Ordinal))
            ? _errors.NotFound(settings.Address)
            : _errors.Other(settings.Address, graphQlErrors);
    }

    private static bool IsAuthCode(string? code) => string.Equals(code, "AUTHENTICATION_ERROR", StringComparison.Ordinal) || string.Equals(code, "FORBIDDEN", StringComparison.Ordinal);

    private Error KeyRefused() => _errors.KeyRefused(settings.Address, keyFix);

    /// <summary>SND-CONTEXT-010 naming Linear's address: Linear is down, slow, rate limiting or unreachable.</summary>
    /// <param name="what">What happened, as a verb phrase.</param>
    /// <returns>The error.</returns>
    public Error Unavailable(string what) => ContextErrors.SourceUnavailable.With($"Linear at {settings.Address} {what}.");

    /// <summary>SND-CONTEXT-072 naming Linear's address: a reply that isn't the shape the operation expects.</summary>
    /// <param name="what">What was wrong, as a verb phrase.</param>
    /// <returns>The error.</returns>
    public Error Unexpected(string what) => ContextErrors.LinearUnexpectedReply.With($"Linear at {settings.Address} {what}; the adapter's queries (D18) may need updating.");

    /// <summary>Logs a failed operation (its name, status and code, never the key or a body) and returns its error.</summary>
    /// <param name="operation">The operation's name.</param>
    /// <param name="error">The error.</param>
    /// <param name="status">The HTTP status, or null when none came.</param>
    /// <returns>The same error.</returns>
    public Error Failed(string operation, Error error, int? status)
    {
        LogFailed(logger, operation, status, error.Code, error.Message);
        return error;
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Linear operation {Operation} failed (HTTP {Status}): {Code} {Reason}")]
    private static partial void LogFailed(ILogger logger, string operation, int? status, string code, string reason);
}
