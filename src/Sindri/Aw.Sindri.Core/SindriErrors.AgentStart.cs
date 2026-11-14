using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-AGENT-012: the repo of a one-off start is not in the profile, or its folder is gone (agents spec §15.8); the message carries the folder path.</summary>
    public static readonly ErrorDefinition AgentRepoUnusable = new("SND-AGENT-012", ErrorKind.Invalid, "The repo is not in the profile, or its folder is gone");

    /// <summary>SND-AGENT-013: the agent of a one-off start is not installed or not signed in (agents spec §15.8); the message carries the sign-in command.</summary>
    public static readonly ErrorDefinition AgentNotReady = new("SND-AGENT-013", ErrorKind.Invalid, "The agent is not installed or not signed in");

    /// <summary>SND-AGENT-014: the one-off worktree could not be made, tmux is not installed, or tmux did not start the session in 4 s (agents spec §15.8); the half-made session and worktree are removed. The spec answers HTTP 503 (Unavailable).</summary>
    public static readonly ErrorDefinition AgentStartFailed = new("SND-AGENT-014", ErrorKind.Unavailable, "The session could not be started");

    /// <summary>SND-AGENT-017: the prompt of a one-off start is over 8,000 characters (agents spec §15.8).</summary>
    public static readonly ErrorDefinition AgentPromptTooLong = new("SND-AGENT-017", ErrorKind.Invalid, "The prompt is over 8,000 characters");

    /// <summary>SND-AGENT-018: the one-off worktree was not removed: it has changes, or it is not a worktree a start made (agents spec §15.8); the message carries the git status summary.</summary>
    public static readonly ErrorDefinition AgentWorktreeKept = new("SND-AGENT-018", ErrorKind.Conflict, "The worktree was not removed");

    /// <summary>SND-AGENT-015: the session has no tmux pane to attach (Warp, IDE, plain terminal; agents spec §15.8); the message carries the host kind.</summary>
    public static readonly ErrorDefinition AgentNoPane = new("SND-AGENT-015", ErrorKind.Conflict, "The session has no tmux pane to attach");

    /// <summary>SND-AGENT-016: the tmux server or the session is gone (agents spec §15.8). The spec says HTTP 410; ErrorKind has no gone kind, so it answers 404 until one is added.</summary>
    public static readonly ErrorDefinition AgentPaneGone = new("SND-AGENT-016", ErrorKind.NotFound, "The tmux server or session is gone");

    private static ErrorDefinition[] EveryAgentStart() => [AgentRepoUnusable, AgentNoPane, AgentPaneGone, AgentNotReady, AgentStartFailed, AgentPromptTooLong, AgentWorktreeKept];
}
