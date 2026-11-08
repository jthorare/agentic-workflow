using Aw.Meta.Core.Config;
using Aw.Meta.Core.Ports;
using Aw.Meta.Core.Usage;

namespace Aw.Meta.Core.Routing;

/// <summary>Prints a ROUTE SUGGEST line when a suggestion appears or changes, once, and keeps what it printed.</summary>
/// <param name="standings">Builds the standings.</param>
/// <param name="values">Keeps what was announced.</param>
public sealed class RouteWatcher(LaneStandingReader standings, IKeyValueStore values)
{
    /// <summary>Takes one look.</summary>
    /// <param name="team">The team.</param>
    /// <param name="readings">The usage look.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The lines to print.</returns>
    public ValueTask<IReadOnlyList<string>> Check(TeamConfig team, IReadOnlyList<UsageReading> readings, CancellationToken cancellationToken)
    {
        _ = (standings, values, readings, cancellationToken);
        ArgumentNullException.ThrowIfNull(team);
        return team.Routing.Equivalents.Count == 0 ? ValueTask.FromResult<IReadOnlyList<string>>([]) : throw new NotSupportedException();
    }
}
