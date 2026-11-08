using Aw.Meta.Core.Config;
using Aw.Meta.Core.Routing;
using Aw.Meta.Core.Usage;

namespace Aw.Meta.Core.Poll;

/// <summary>One watch poll's usage work: look at the lanes and the meta, add each provider's own signal, then apply the rules.</summary>
/// <param name="reader">Looks at the lanes and the meta.</param>
/// <param name="standings">Adds provider, model and usage windows.</param>
/// <param name="watcher">Prints, remembers, logs and resumes.</param>
/// <param name="routes">Suggests moving lanes that are running out to another provider.</param>
public sealed class UsageMonitor(UsageReader reader, LaneStandingReader standings, UsageWatcher watcher, RouteWatcher routes)
{
    /// <summary>Takes one look and applies the rules.</summary>
    /// <param name="team">The team.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The lines to print; none when the look could not be taken (advisory: the poll goes on without usage).</returns>
    public async ValueTask<IReadOnlyList<string>> Check(TeamConfig team, CancellationToken cancellationToken)
    {
        if (await reader.ReadOrNull(team, cancellationToken).ConfigureAwait(false) is not { } readings)
        {
            return [];
        }

        var looked = await standings.Read(team, readings, cancellationToken).ConfigureAwait(false);
        var usage = await watcher.Tick(team, StandingReadings.Merge(readings, looked), cancellationToken).ConfigureAwait(false);
        return [.. usage, .. await routes.Check(team, looked, cancellationToken).ConfigureAwait(false)];
    }
}
