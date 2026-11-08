namespace Aw.Sindri.Core.Graph.Runs;

/// <summary>Where one node's execution stands.</summary>
public enum NodeRunStatus
{
    /// <summary>The block is executing.</summary>
    Running = 1,

    /// <summary>Waiting on a person, a timer or an agent.</summary>
    Waiting,

    /// <summary>It finished and has an output.</summary>
    Succeeded,

    /// <summary>It failed; 'Error' says why.</summary>
    Failed,

    /// <summary>An upstream failure or a branch not taken meant it never ran.</summary>
    Skipped,

    /// <summary>The run was cancelled while it ran.</summary>
    Cancelled,
}
