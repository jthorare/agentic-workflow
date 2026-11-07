using Aw.Kernel;

namespace Aw.Context.Infrastructure.Linear;

/// <summary>
/// Which catalogue a call to Linear answers in (Linear hub spec 2026-11-04 §8): the read key's SND-CONTEXT codes or the write key's SND-LINEAR codes. <see cref="LinearCalls"/> classifies the reply, this says what each class is called. A body of the wrong shape is
/// SND-CONTEXT-072 in both.
/// </summary>
internal abstract class LinearErrorSet
{
    /// <summary>The read key's catalogue; its messages are the read client's, unchanged.</summary>
    public static LinearErrorSet Read { get; } = new LinearReadErrors();

    /// <summary>The write key's catalogue.</summary>
    public static LinearErrorSet Write { get; } = new LinearWriteErrors();

    /// <summary>Linear refused the key (401, 403, or an authentication error inside a reply).</summary>
    /// <param name="address">Linear's host and port.</param>
    /// <param name="fix">What to do about it, as the caller gives it.</param>
    /// <returns>The error.</returns>
    public abstract Error KeyRefused(string address, string fix);

    /// <summary>Linear rate limited the call (HTTP 429, or a RATELIMITED error).</summary>
    /// <param name="address">Linear's host and port.</param>
    /// <param name="resetHeader">The 'x-ratelimit-requests-reset' header (epoch milliseconds), or null.</param>
    /// <returns>The error.</returns>
    public abstract Error RateLimited(string address, string? resetHeader);

    /// <summary>Linear answered with a 5xx.</summary>
    /// <param name="address">Linear's host and port.</param>
    /// <param name="operation">The operation's name.</param>
    /// <param name="status">The HTTP status.</param>
    /// <returns>The error.</returns>
    public abstract Error ServerFailed(string address, string operation, int status);

    /// <summary>Every GraphQL error says an entity wasn't found.</summary>
    /// <param name="address">Linear's host and port.</param>
    /// <returns>The error.</returns>
    public abstract Error NotFound(string address);

    /// <summary>GraphQL errors that are none of the above.</summary>
    /// <param name="address">Linear's host and port.</param>
    /// <param name="errors">The errors, at least one.</param>
    /// <returns>The error.</returns>
    public abstract Error Other(string address, IReadOnlyList<LinearGraphQlError> errors);
}
