namespace Aw.Sindri.Core.Connections.Polling;

/// <summary>
/// What one provider call gave back. A rate-limited answer (429, 5xx) is not an error: it has no events, keeps the cursor and
/// says how long the provider asked to wait, so the caller can store the backoff.
/// </summary>
/// <param name="Events">The events of the asked kind, oldest first; empty when rate limited.</param>
/// <param name="NextCursor">The cursor to save once the runs for these events started; the asked-for cursor when rate limited.</param>
/// <param name="RateLimited">True when the provider answered 429 or 5xx.</param>
/// <param name="RetryAfter">The provider's Retry-After (or the wait until its rate-limit reset), or null.</param>
/// <param name="Gap">True when the poller hit its page cap before reaching the saved cursor: events older than the pages it read are not in <paramref name="Events"/>, and the cursor moved past them. The caller logs it (catchup.capped).</param>
public sealed record PollResult(IReadOnlyList<PolledEvent> Events, string? NextCursor, bool RateLimited, TimeSpan? RetryAfter, bool Gap = false);
