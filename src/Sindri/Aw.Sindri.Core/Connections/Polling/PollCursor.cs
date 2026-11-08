namespace Aw.Sindri.Core.Connections.Polling;

/// <summary>Where polling stands for one (connection, event kind): the saved cursor, the last poll and the backoff.</summary>
/// <param name="Connection">The connection.</param>
/// <param name="Kind">The event kind.</param>
/// <param name="Value">The provider cursor (an event id or a timestamp), or null before the first poll.</param>
/// <param name="UpdatedAt">When the cursor last moved, or null.</param>
/// <param name="LastPollAt">When the provider was last called, or null.</param>
/// <param name="BackoffUntil">No poll before this instant, or null.</param>
/// <param name="Failures">How many polls in a row were rate limited or failed; 0 after a good one.</param>
public sealed record PollCursor(
    ConnectionName Connection,
    EventKind Kind,
    string? Value,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? LastPollAt,
    DateTimeOffset? BackoffUntil,
    int Failures);
