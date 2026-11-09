namespace Aw.Sindri.Core.Events;

/// <summary>
/// The 'catchup.capped' payload (#2258, PR 11): a catch-up read its page cap before it reached the saved cursor, so older events were skipped. One event per capped (connection, event kind) read;
/// the cursor moved to the newest event read.
/// </summary>
/// <param name="Connection">The connection name.</param>
/// <param name="Kind">The event kind.</param>
/// <param name="Reason">Why, in words for the owner ('history order', or 'more than N pages').</param>
/// <param name="At">When the catch-up stopped.</param>
public sealed record CatchUpCapped(string Connection, string Kind, string Reason, DateTimeOffset At);
