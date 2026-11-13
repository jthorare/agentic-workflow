using Aw.Meta.Core.Events;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>
/// Writes obligation.opened and obligation.closed. <c>lane</c> is the sender's session name without the "&lt;team&gt;-" prefix
/// ("t1-api" is lane "api"); <c>from</c> keeps the full name. A failed append never fails the caller.
/// </summary>
/// <param name="events">Appends the events.</param>
/// <param name="config">Names the team and the meta lane.</param>
/// <param name="reader">Tells which replies are still owed.</param>
/// <param name="clock">Dates a closed event.</param>
public sealed class ObligationEventWriter(MetaEventSink events, IMetaConfig config, ObligationReader reader, TimeProvider clock)
{
    /// <summary>Tells whether the message is an open reply obligation right now (not dismissed, not answered, not a notice).</summary>
    /// <param name="id">The message id.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>True when a reply to it is owed.</returns>
    public async ValueTask<bool> IsOwed(string id, CancellationToken cancellationToken)
    {
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (team.IsError)
        {
            return false;
        }

        var open = await reader.Open(team.Value.MetaLane, clock.GetUtcNow(), cancellationToken).ConfigureAwait(false);
        return open.IsSuccess && open.Value.Any(o => o.Kind == ObligationKind.Reply && string.Equals(o.Key, id, StringComparison.Ordinal));
    }

    /// <summary>Appends obligation.opened.</summary>
    /// <param name="at">When the message arrived.</param>
    /// <param name="id">The message id.</param>
    /// <param name="sender">The sender's session name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that ends when the append ended.</returns>
    public async ValueTask Opened(DateTimeOffset at, string id, string sender, CancellationToken cancellationToken) =>
        await events.Emit(MetaEvents.ObligationOpened(at, id, sender, await Lane(sender, cancellationToken).ConfigureAwait(false)), cancellationToken).ConfigureAwait(false);

    /// <summary>Appends obligation.closed, dated now.</summary>
    /// <param name="id">The message id.</param>
    /// <param name="sender">The sender's session name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>A task that ends when the append ended.</returns>
    public async ValueTask Closed(string id, string sender, CancellationToken cancellationToken) =>
        await events.Emit(MetaEvents.ObligationClosed(clock.GetUtcNow(), id, sender, await Lane(sender, cancellationToken).ConfigureAwait(false)), cancellationToken).ConfigureAwait(false);

    private async ValueTask<string> Lane(string sender, CancellationToken cancellationToken)
    {
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        return team.IsError ? sender : LaneNames.WithoutTeam(sender, team.Value.Team);
    }
}
