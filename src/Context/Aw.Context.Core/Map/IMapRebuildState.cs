namespace Aw.Context.Core.Map;

/// <summary>
/// Where the map's rebuild stands (concept pins spec §3.7): whether a refresh is queued or running, and when none is, why the refresh service isn't starting one.
/// Context only asks; the host that runs the refresh service answers, so Context needs no reference to it.
/// </summary>
public interface IMapRebuildState
{
    /// <summary>Reads the rebuild's state now.</summary>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The state; <see cref="MapRebuildStatus.Idle"/> when nothing is known or nothing blocks.</returns>
    public ValueTask<MapRebuildStatus> Read(CancellationToken cancellationToken);
}
