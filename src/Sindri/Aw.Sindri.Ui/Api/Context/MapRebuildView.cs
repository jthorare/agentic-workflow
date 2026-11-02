namespace Aw.Sindri.Ui.Api.Context;

/// <summary>Why the map is, or is not, being rebuilt (the server's map rebuild state, concept pins spec §3.7).</summary>
/// <param name="Phase">One of <see cref="MapRebuildPhases"/>.</param>
/// <param name="Progress">The running refresh's latest progress line ("resolving calls"), or null.</param>
/// <param name="RetryAt">When the backoff after failed refreshes ends; set only for "backoff".</param>
/// <param name="Stale">Whether the current map was built under other rules than the approved profile's (a pin change it doesn't have yet).</param>
/// <param name="Failed">The code of the last refresh when it failed and none succeeded since ("SND-CONTEXT-…"), or null.</param>
internal sealed record MapRebuildView(string Phase, string? Progress, DateTimeOffset? RetryAt, bool Stale, string? Failed);
