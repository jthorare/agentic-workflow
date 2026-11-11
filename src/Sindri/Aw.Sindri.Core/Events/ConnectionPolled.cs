namespace Aw.Sindri.Core.Events;

/// <summary>
/// The 'connection.polled' payload (#2258, PR 11): one poll of a connection for one event kind that returned events. Counts only, never provider text.
/// A poll that returned nothing writes no event.
/// </summary>
/// <param name="Connection">The connection name ('github:work').</param>
/// <param name="Kind">The event kind ('github.pr.merged').</param>
/// <param name="Events">How many events the provider returned.</param>
/// <param name="Started">How many workflow runs the events started or queued.</param>
/// <param name="Skipped">How many triggers were skipped (a run in flight, or the delivery already had its run).</param>
/// <param name="At">When the poll ended.</param>
public sealed record ConnectionPolled(string Connection, string Kind, int Events, int Started, int Skipped, DateTimeOffset At);
