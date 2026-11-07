using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The shell commands a Step session's hooks run to tell Sindri when its turn ends (the Stop hook touches launches/&lt;launch&gt;.turn-ended; StopFailure touches .turn-failed) and starts (a prompt or a tool call deletes those
/// files), so a session that ends its turn with no report can be ended as idle and one an API error stopped can be nudged first. A tool call in flight is marked (.tool-running, from PreToolUse until PostToolUse, PostToolUseFailure
/// or the turn's end), so a long build is never taken for an idle session. A skill block's session also marks each prompt (.prompted). The commands never print, never fail the hook, and do nothing outside a launch (no
/// AW_SINDRI_LAUNCH or AW_SINDRI_HOME). Claude Code reads them from its settings file; Codex from the worktree's '.codex/hooks.json' (<see cref="CodexTurnHooks"/>).
/// </summary>
internal static class TurnHookCommands
{
    private const string Prefix = "\"$AW_SINDRI_HOME/" + LaunchFiles.Directory + "/$AW_SINDRI_LAUNCH";
    private const string Ended = Prefix + LaunchFiles.TurnEndedSuffix + "\"";
    private const string Failed = Prefix + LaunchFiles.TurnFailedSuffix + "\"";
    private const string Tool = Prefix + LaunchFiles.ToolRunningSuffix + "\"";
    private const string Prompted = Prefix + LaunchFiles.PromptedSuffix + "\"";
    private const string Guard = "if [ -n \"$AW_SINDRI_LAUNCH\" -a -n \"$AW_SINDRI_HOME\" ]; then ";
    private const string MakeDirectory = "mkdir -p \"$AW_SINDRI_HOME/" + LaunchFiles.Directory + "\"; ";

    /// <summary>The turn ended: mark it, and no tool is running.</summary>
    internal static string Stop => Run(MakeDirectory + "touch " + Ended + "; rm -f " + Tool);

    /// <summary>An API error ended the turn.</summary>
    internal static string StopFailure => Run(MakeDirectory + "touch " + Failed + "; rm -f " + Tool);

    /// <summary>A prompt started a turn: the end markers go; a skill block's session also marks the prompt.</summary>
    /// <param name="markers">True for a skill block's session.</param>
    /// <returns>The command.</returns>
    internal static string Prompt(bool markers) => Run(markers ? MakeDirectory + "rm -f " + Ended + "; rm -f " + Failed + "; touch " + Prompted : "rm -f " + Ended + "; rm -f " + Failed);

    /// <summary>A tool call starts: the turn is running again, and a tool is in flight.</summary>
    internal static string PreTool => Run(MakeDirectory + "rm -f " + Ended + "; rm -f " + Failed + "; touch " + Tool);

    /// <summary>A tool call returned or failed.</summary>
    internal static string PostTool => Run("rm -f " + Tool);

    private static string Run(string commands) => Guard + commands + "; fi; exit 0";
}
