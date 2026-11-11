using Aw.Meta.Core.Config;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Watch;

namespace Aw.Meta.Features.Watch;

/// <summary>
/// The watch's LANE DOWN / LANE UP lines (#2856): each poll compares the lanes with no live session with what was kept,
/// and keeps the new state whenever it changes. Not shared between watches.
/// </summary>
/// <param name="team">The team polled.</param>
/// <param name="missing">This poll's lanes with no live session; null when they can't be read (the poll then says nothing about them).</param>
/// <param name="keep">Keeps the state whenever it changes.</param>
/// <param name="kept">The state an earlier watch kept; null before the first poll on a box.</param>
internal sealed class LaneSessions(TeamConfig team, Func<ValueTask<IReadOnlyList<string>?>> missing, Func<LaneWatch, ValueTask> keep, LaneWatch? kept)
{
    private LaneWatch? _kept = kept;
    private IReadOnlySet<string> _pending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    internal async ValueTask<IReadOnlyList<string>> Poll()
    {
        if (await missing().ConfigureAwait(false) is not { } gone)
        {
            return [];
        }

        var (lines, state, pending) = LaneWatch.Step(_kept, _pending, MissingLanes.Wanted(team), gone);
        _pending = pending;
        if (_kept is null || !string.Equals(state.Format(), _kept.Format(), StringComparison.Ordinal))
        {
            _kept = state;
            await keep(state).ConfigureAwait(false);
        }

        return lines;
    }
}
