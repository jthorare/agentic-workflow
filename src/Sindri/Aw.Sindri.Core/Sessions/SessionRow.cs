using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Sessions;

/// <summary>
/// One live session as a client sees it (collector spec CA-1). Never in a row: the transcript path, the cwd, the environment, the messaging socket, the bridge session id
/// or the socket path. <see cref="Title"/> and <see cref="Pid"/> are for human callers only.
/// </summary>
/// <param name="Ref">'&lt;provider&gt;/&lt;id&gt;'.</param>
/// <param name="Provider">The agent.</param>
/// <param name="ProviderLabel">What a person calls it ('Claude Code').</param>
/// <param name="Id">The session id.</param>
/// <param name="IdKnown">False for a 'pid-…' id.</param>
/// <param name="State">What it is doing.</param>
/// <param name="Asking">Why it waits, when it is asking.</param>
/// <param name="StateSource">Where the state came from.</param>
/// <param name="Estimated">True when the state is a guess from the transcript's age.</param>
/// <param name="Repo">The profile repo it works in.</param>
/// <param name="Worktree">Its directory relative to the repo's checkout, when below it.</param>
/// <param name="Host">What shows it.</param>
/// <param name="Lane">Its Meta lane.</param>
/// <param name="LaunchId">The Sindri launch it belongs to.</param>
/// <param name="StartedAt">When the session started.</param>
/// <param name="Since">When the current state began.</param>
/// <param name="Focus">Whether Focus can run.</param>
public sealed record SessionRow(
    string Ref, AgentId Provider, string ProviderLabel, SessionId Id, bool IdKnown, SessionState State, SessionAsking? Asking, StateSource StateSource, bool Estimated,
    RepoName? Repo, string? Worktree, SessionHostRow Host, SessionLane? Lane, LaunchId? LaunchId, DateTimeOffset? StartedAt, DateTimeOffset Since, SessionFocus Focus)
{
    /// <summary>The session's title, scrubbed and capped (human callers only).</summary>
    public string? Title { get; init; }

    /// <summary>The workflow instance, Step and attempt of the Sindri launch this session belongs to; null when no launch of Sindri's is known for it.</summary>
    public LaunchStep? Launch { get; init; }

    /// <summary>The agent's process id (human callers only).</summary>
    public int? Pid { get; init; }

    /// <summary>What the sandbox says about the session (sandbox spec §8); null, and left out, for a session whose launch was not sandboxed or that has no launch of Sindri's.</summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Sandbox.SessionSandbox? Sandbox { get; init; }
}
