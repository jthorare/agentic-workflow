namespace Aw.Sindri.Core.Graph;

/// <summary>The states a 'run_agent' node's session reports in 'workflow.node.agent.changed' (#3151).</summary>
public static class NodeAgentStates
{
    /// <summary>The node waits for a launch slot; its timeout has not started.</summary>
    public const string WaitingForSlot = "waiting-for-slot";

    /// <summary>The launch is under way.</summary>
    public const string Launching = "launching";

    /// <summary>The session is working.</summary>
    public const string Working = "working";

    /// <summary>The session waits on a person.</summary>
    public const string WaitingOnYou = "waiting-on-you";

    /// <summary>The agent hit a usage limit.</summary>
    public const string Limited = "limited";

    /// <summary>The session ended and the node succeeded.</summary>
    public const string EndedSucceeded = "ended-succeeded";

    /// <summary>The session ended and the node failed.</summary>
    public const string EndedFailed = "ended-failed";

    /// <summary>The node's timeout ended the session.</summary>
    public const string EndedTimedOut = "ended-timed-out";
}
