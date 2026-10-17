using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Launches;

/// <summary>
/// The heartbeats of the launches this process has seen (API spec §8.1), in memory: a restart forgets them and the next heartbeat starts the clock again,
/// so a session that outlives the control plane is not ended by the restart. It also remembers which launches already reported their session id.
/// </summary>
public sealed class LaunchLiveness
{
    private readonly Lock _gate = new();
    private readonly Dictionary<LaunchId, DateTimeOffset> _last = [];
    private readonly HashSet<LaunchId> _reported = [];

    /// <summary>Records a heartbeat (or starts the clock for a launch the launcher just started).</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="at">When.</param>
    public void Beat(LaunchId launch, DateTimeOffset at)
    {
        lock (_gate)
        {
            _last[launch] = at;
        }
    }

    /// <summary>Notes that the launch's session id was recorded.</summary>
    /// <param name="launch">The launch.</param>
    public void MarkReported(LaunchId launch)
    {
        lock (_gate)
        {
            _ = _reported.Add(launch);
        }
    }

    /// <summary>Whether the launch's session id was recorded in this process.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>True after <see cref="MarkReported"/>.</returns>
    public bool IsReported(LaunchId launch)
    {
        lock (_gate)
        {
            return _reported.Contains(launch);
        }
    }

    /// <summary>Forgets the launch: its session ended.</summary>
    /// <param name="launch">The launch.</param>
    public void End(LaunchId launch)
    {
        lock (_gate)
        {
            _ = _last.Remove(launch);
            _ = _reported.Remove(launch);
        }
    }

    /// <summary>The launches this process is tracking: started or beating, and not ended.</summary>
    /// <returns>The launches, in no set order.</returns>
    public IReadOnlyList<LaunchId> Active()
    {
        lock (_gate)
        {
            return [.. _last.Keys];
        }
    }

    /// <summary>The launches whose last heartbeat is at least <paramref name="silence"/> old.</summary>
    /// <param name="now">The time now.</param>
    /// <param name="silence">How long without a heartbeat.</param>
    /// <returns>The silent launches, oldest silence first.</returns>
    public IReadOnlyList<LaunchId> Silent(DateTimeOffset now, TimeSpan silence)
    {
        lock (_gate)
        {
            return [.. _last.Where(l => now - l.Value >= silence).OrderBy(static l => l.Value).Select(static l => l.Key)];
        }
    }
}
