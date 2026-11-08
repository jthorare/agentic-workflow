namespace Aw.Sindri.Core.Setup;

/// <summary>Where the context map stands.</summary>
public enum MapPhase
{
    /// <summary>Nothing to build from yet: no approved profile, or no repo in it.</summary>
    Waiting,

    /// <summary>No map, and no build has run.</summary>
    NotBuilt,

    /// <summary>A build is running.</summary>
    Building,

    /// <summary>No map, and the last build failed.</summary>
    Failed,

    /// <summary>A current map exists.</summary>
    Built,
}
