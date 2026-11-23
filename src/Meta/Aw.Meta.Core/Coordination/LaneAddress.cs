using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>
/// Finds the sender address of the Claude session a lane runs now, so a message from the lane is known after its session was renamed
/// (#4103): lane-recycle's /clear leaves the digest prompt as the name, and an account switch drops it too.
/// </summary>
/// <param name="files">The lane records (lane-sessions/&lt;lane&gt;).</param>
/// <param name="sessions">The session the Claude in the lane's tmux session runs now.</param>
/// <param name="live">The running sessions, with their addresses.</param>
public sealed class LaneAddress(ILaneFiles files, ILaneSessions sessions, ISessionDirectory live)
{
    /// <summary>Finds the address of the lane's running session.</summary>
    /// <param name="team">The team name (tmux sessions are "team-lane").</param>
    /// <param name="lane">The lane name.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The address; null when the lane has no running session with a socket, or a source can't be read.</returns>
    public async ValueTask<string?> Find(string team, string lane, CancellationToken cancellationToken)
    {
        // The context reader's order: Claude changes the id on /clear, so the id under the lane's tmux session comes first, then the
        // id lane-sessions recorded. A source that can't be read finds nothing, and the caller still matches by name.
        var current = await sessions.Current(team + "-" + lane, cancellationToken).ConfigureAwait(false);
        var id = current.IsSuccess ? current.Value : null;
        if (id is null)
        {
            var record = await files.Record(lane, cancellationToken).ConfigureAwait(false);
            id = record.IsSuccess ? record.Value?.Session : null;
        }

        if (id is not { } session)
        {
            return null;
        }

        var running = await live.Live(cancellationToken).ConfigureAwait(false);
        return running.IsSuccess ? running.Value.FirstOrDefault(s => s.Session == session)?.Address : null;
    }
}
