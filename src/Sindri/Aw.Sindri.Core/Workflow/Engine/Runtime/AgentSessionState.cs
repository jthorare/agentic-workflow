namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>Whether an agent Step's session is still running, as the watch sees it (engine P8 D3): live, or ended by an exit or a lost heartbeat.</summary>
/// <param name="EndedAs">How the session ended, or null while it runs.</param>
/// <param name="ExitCode">The agent's exit code for an exit, otherwise null.</param>
/// <param name="Detail">Why the engine ended the session, as one scrubbed line (a refused report), otherwise null; the failed Step carries it. Absent from a result recorded before it existed.</param>
/// <param name="Refused">True when <paramref name="Detail"/> is the text of a refused report (model-pick spec 7.1: the failure is "refused", not a crash).</param>
public sealed record AgentSessionState(AgentSessionEnd? EndedAs = null, int? ExitCode = null, string? Detail = null, bool Refused = false)
{
    /// <summary>The session has not ended.</summary>
    public static AgentSessionState Live { get; } = new();

    /// <summary>Whether the session is still running.</summary>
    public bool IsLive => EndedAs is null;
}
