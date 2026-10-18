using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Sessions;

/// <summary>One state file of the aw:fleet hook (collector spec §5.1, schemas/fleet-state.schema.json): the latest hook event of one session. It holds no prompt or tool text.</summary>
/// <param name="Provider">The agent.</param>
/// <param name="Session">The session id.</param>
/// <param name="State">The state the event maps to.</param>
/// <param name="At">When the hook ran.</param>
public sealed record FleetStateFile(AgentId Provider, SessionId Session, HookState State, DateTimeOffset At)
{
    /// <summary>Why the session is asking, when it is.</summary>
    public AskingReason? Reason { get; init; }

    /// <summary>The tool an approval is for ('Bash'), never its input.</summary>
    public string? Tool { get; init; }

    /// <summary>The working directory.</summary>
    public string? Cwd { get; init; }

    /// <summary>The transcript's path (only ever stat'ed).</summary>
    public string? Transcript { get; init; }

    /// <summary>The agent's process id, where the hook could tell.</summary>
    public int? AgentPid { get; init; }

    /// <summary>The agent's start ticks; present exactly when <see cref="AgentPid"/> is.</summary>
    public long? AgentStartTicks { get; init; }
}
