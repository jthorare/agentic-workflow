namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Why the map is, or is not, being rebuilt (concept pins spec §3.7).</summary>
/// <param name="Phase">'idle', 'building', 'mapHeld', 'noMainBranch' or 'backoff'.</param>
/// <param name="Progress">The running refresh's latest progress line, or null.</param>
/// <param name="RetryAt">When the backoff ends; set only for 'backoff'.</param>
/// <param name="Stale">Whether the current map was built under other rules than the approved profile's (a map is held for review: not stale, it is on its way).</param>
/// <param name="Failed">The code of the last RefreshContextMap job when it failed and none succeeded since, or null.</param>
internal sealed record ContextRebuildView(string Phase, string? Progress, DateTimeOffset? RetryAt, bool Stale, string? Failed);
