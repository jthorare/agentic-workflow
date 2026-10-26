using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Launch;

/// <summary>The names of the flat per-launch files under <c lang="text">$AW_SINDRI_HOME/launches/</c> (launcher L-9): shared by the launcher, 'session wrap' and the id hook.</summary>
public static class LaunchFiles
{
    /// <summary>The directory under Sindri's home.</summary>
    public const string Directory = "launches";

    /// <summary>The capture rule's file name for <paramref name="launch"/>.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>'&lt;id&gt;.capture.json'.</returns>
    public static string CaptureName(LaunchId launch) => launch.Value + ".capture.json";

    /// <summary>The file the id hook writes for <paramref name="launch"/>.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>'&lt;id&gt;.session-id'.</returns>
    public static string SessionIdName(LaunchId launch) => launch.Value + ".session-id";

    /// <summary>The end of the name of the file a Step session's Stop hook touches when its turn ends (and its prompt hook deletes when a new turn starts).</summary>
    public const string TurnEndedSuffix = ".turn-ended";

    /// <summary>The file the Stop hook touches for <paramref name="launch"/>.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>'&lt;id&gt;.turn-ended'.</returns>
    public static string TurnEndedName(LaunchId launch) => launch.Value + TurnEndedSuffix;

    /// <summary>The end of the name of the file the StopFailure hook touches when an API error ends the turn (a new prompt deletes it).</summary>
    public const string TurnFailedSuffix = ".turn-failed";

    /// <summary>The file the StopFailure hook touches for <paramref name="launch"/>.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>'&lt;id&gt;.turn-failed'.</returns>
    public static string TurnFailedName(LaunchId launch) => launch.Value + TurnFailedSuffix;

    /// <summary>The end of the name of the file the PreToolUse hook touches while a tool call runs (PostToolUse, PostToolUseFailure, Stop and StopFailure delete it).</summary>
    public const string ToolRunningSuffix = ".tool-running";

    /// <summary>The file the PreToolUse hook touches for <paramref name="launch"/>.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>'&lt;id&gt;.tool-running'.</returns>
    public static string ToolRunningName(LaunchId launch) => launch.Value + ToolRunningSuffix;

    /// <summary>The end of the name of the file the idle sweep touches when it sent a stalled session its one re-prompt.</summary>
    public const string NudgedSuffix = ".nudged";

    /// <summary>The file the idle sweep touches for <paramref name="launch"/> after its re-prompt.</summary>
    /// <param name="launch">The launch.</param>
    /// <returns>'&lt;id&gt;.nudged'.</returns>
    public static string NudgedName(LaunchId launch) => launch.Value + NudgedSuffix;
}
