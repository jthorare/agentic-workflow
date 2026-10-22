namespace Aw.Sindri.Core.Onyx;

/// <summary>Where the Onyx setup job stands (setup-once spec §5.6), as the Onyx step reads it from the jobs view.</summary>
public enum OnyxJobPhase
{
    /// <summary>No job is under way and none ended in a way the step shows.</summary>
    None,

    /// <summary>The job is running: it brings the stack up, or finishes the connection with the login it was given.</summary>
    Working,

    /// <summary>The job waits on 'onyx-account' for the owner's login.</summary>
    WaitingForLogin,

    /// <summary>The latest job failed for a reason that is not the owner's choice (a timeout or a restart is not one).</summary>
    Failed,
}
