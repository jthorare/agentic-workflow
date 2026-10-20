namespace Aw.Sindri.Core.Scheduling;

/// <summary>What a main-move service does on a tick (the context map refresh, and later the code index build).</summary>
public enum MainMoveAction
{
    /// <summary>Nothing moved: a head read and nothing else.</summary>
    Nothing,

    /// <summary>A job of the kind is running: never doubled.</summary>
    JobRunning,

    /// <summary>The last job(s) failed and the wait is not over.</summary>
    BackoffNotDue,

    /// <summary>Start one job.</summary>
    Start,
}
