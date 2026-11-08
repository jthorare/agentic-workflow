namespace Aw.Sindri.Infrastructure.Connections.Polling;

/// <summary>What one provider call answered: a JSON body, or a rate-limited answer with the wait the provider named.</summary>
/// <param name="Body">The response body, or null when rate limited.</param>
/// <param name="RateLimited">True for 429, 5xx and a provider that could not be reached.</param>
/// <param name="RetryAfter">The Retry-After in seconds, or null.</param>
internal sealed record PollerAnswer(string? Body, bool RateLimited, TimeSpan? RetryAfter);
