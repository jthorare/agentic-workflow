using Aw.Kernel;

namespace Aw.Sindri.Core.Connections.Polling;

/// <summary>One provider's event feed over plain HTTP with the connection's token. It never sends the token to a host outside the preset.</summary>
public interface IEventPoller
{
    /// <summary>Gets the preset key this poller serves ('github').</summary>
    public string Preset { get; }

    /// <summary>
    /// Asks the provider for the events of one kind after a cursor. A null cursor is a baseline: it returns NO events and
    /// <see cref="PollResult.NextCursor"/> = the newest position on the provider (or now, when the provider has none), so adding a trigger
    /// never starts runs for old events. A cursor reads forward, oldest event first, page by page up to <see cref="PollLimits.DefaultPageCap"/>
    /// pages; past that <see cref="PollResult.Gap"/> is true. On a gap the cursor still moves to the newest event read; older events are skipped, and the caller logs catchup.capped. A poller may return an event it returned before (an overlap that covers a late feed):
    /// the delivery key is the run's occurrence id, and the caller's duplicate-id refusal drops the repeat.
    /// </summary>
    /// <param name="connection">The connection (its secret pointer names the token).</param>
    /// <param name="kind">The event kind.</param>
    /// <param name="cursor">The saved cursor, or null for the first poll.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The events and next cursor (or a rate-limited result), SND-CONN-006 when the token is unreadable, SND-CONN-012 when the answer is not the shape the poller knows or a redirect leaves the preset host, SND-CONN-013 when the provider rejects the cursor.</returns>
    public ValueTask<Result<PollResult>> Poll(Connection connection, EventKind kind, string? cursor, CancellationToken cancellationToken);
}
