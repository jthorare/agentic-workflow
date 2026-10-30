namespace Aw.Context.Core.Map;

/// <summary>Why the map is, or is not, being rebuilt.</summary>
public enum MapRebuildPhase
{
    /// <summary>No refresh runs and nothing blocks one.</summary>
    Idle,

    /// <summary>A refresh is queued or running.</summary>
    Building,

    /// <summary>A built map is held for review, so the service starts no refresh.</summary>
    MapHeld,

    /// <summary>No mapped repo is on its main branch.</summary>
    NoMainBranch,

    /// <summary>The service waits out the backoff after failed refreshes (<see cref="MapRebuild.RetryAt"/>).</summary>
    Backoff,
}
