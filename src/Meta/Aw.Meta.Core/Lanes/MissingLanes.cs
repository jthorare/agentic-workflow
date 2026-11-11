using Aw.Kernel;
using Aw.Meta.Core.Config;
using Aw.Meta.Core.Messages;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Lanes;

/// <summary>Finds the configured lanes that have no live session (health's missing lanes, the watch's LANE DOWN). Read-only: the lane map itself is not changed.</summary>
/// <param name="sessions">Live sessions.</param>
/// <param name="messages">The recorded lane announcements.</param>
/// <param name="lanes">The current lane map.</param>
public sealed class MissingLanes(ISessionDirectory sessions, IMessageStore messages, ILaneStore lanes)
{
    /// <summary>The team's lanes (the meta lane left out) that no live session maps to.</summary>
    /// <param name="team">The team config.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The lanes, in config order.</returns>
    public async ValueTask<Result<IReadOnlyList<string>>> Find(TeamConfig team, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(team);
        var live = await sessions.Live(cancellationToken).ConfigureAwait(false);
        var inbox = await messages.Messages(cancellationToken).ConfigureAwait(false);
        var previous = await lanes.Current(cancellationToken).ConfigureAwait(false);
        if (live.IsError || inbox.IsError || previous.IsError)
        {
            return live.IsError ? live.Error : inbox.IsError ? inbox.Error : previous.Error;
        }

        var wanted = Wanted(team);
        var announcements = inbox.Value.Where(m => m.Class.Kind == MessageKind.LaneAnnounce).ToList();
        var mapped = LaneMap.Refresh(team.Team, wanted, live.Value, announcements, previous.Value).Entries.Select(e => e.Lane).ToList();
        return Results.Success<IReadOnlyList<string>>([.. wanted.Where(lane => !mapped.Contains(lane, StringComparer.OrdinalIgnoreCase))]);
    }

    /// <summary>The team's lanes with the meta lane left out.</summary>
    /// <param name="team">The team config.</param>
    /// <returns>The lanes, in config order.</returns>
    public static IReadOnlyList<string> Wanted(TeamConfig team)
    {
        ArgumentNullException.ThrowIfNull(team);
        return [.. team.Lanes.Where(lane => !string.Equals(lane, team.MetaLane, StringComparison.OrdinalIgnoreCase))];
    }
}
