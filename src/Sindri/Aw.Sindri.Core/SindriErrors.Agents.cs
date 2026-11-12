using Aw.Kernel;

namespace Aw.Sindri.Core;

public static partial class SindriErrors
{
    /// <summary>SND-AGENT-001: no Agents row has that id (Agents page spec §9).</summary>
    public static readonly ErrorDefinition AgentRowNotFound = new("SND-AGENT-001", ErrorKind.NotFound, "No such Agents row (fix: sindri agents lists the rows)");

    /// <summary>SND-AGENT-002: a steer names an old row version; the error carries the current one (Agents page spec §9).</summary>
    public static readonly ErrorDefinition AgentVersionStale = new("SND-AGENT-002", ErrorKind.Conflict, "The row changed since it was read (fix: read the row again and send the current version)");

    /// <summary>SND-AGENT-003: the op does not apply to this row; the detail carries the reason sentence (Agents page spec §9).</summary>
    public static readonly ErrorDefinition AgentOpNotApplicable = new("SND-AGENT-003", ErrorKind.Invalid, "That op does not apply to this row");

    /// <summary>SND-AGENT-004: Stop refused because the process id or start ticks changed since the row was read (Agents page spec §9).</summary>
    public static readonly ErrorDefinition AgentStopRefused = new("SND-AGENT-004", ErrorKind.Conflict, "Stop refused: the process is not the one the row showed (fix: read the row again)");

    /// <summary>SND-AGENT-005: recycle refused because the digest is missing, unreadable, not absolute, over 4,096 characters, starts with '-', or is older than the lane's session or 2 hours (Agents page spec §9).</summary>
    public static readonly ErrorDefinition AgentRecycleRefused = new("SND-AGENT-005", ErrorKind.Invalid, "Recycle refused: the digest is not usable (fix: write a fresh digest and pass its absolute path)");

    /// <summary>SND-AGENT-006: <c lang="text">aw-meta</c> is unavailable: no team, no binary, or exit 2 (Agents page spec §9).</summary>
    public static readonly ErrorDefinition AgentMetaUnavailable = new("SND-AGENT-006", ErrorKind.Unavailable, "aw-meta is unavailable (fix: install it with ./setup.sh --with-meta and check the team with aw-meta team-init)");

    /// <summary>SND-AGENT-007: the message is empty or over 8,000 characters (Agents page spec §9).</summary>
    public static readonly ErrorDefinition AgentMessageInvalid = new("SND-AGENT-007", ErrorKind.Invalid, "A message is 1 to 8,000 characters");

    /// <summary>SND-AGENT-008: the session's activity could not be read: the transcript is missing or locked (Agents page spec §9).</summary>
    public static readonly ErrorDefinition AgentActivityUnreadable = new("SND-AGENT-008", ErrorKind.Unavailable, "The session's activity could not be read (fix: try again; the transcript may be locked)");

    /// <summary>SND-AGENT-009: an earlier op on this row is still sending, or an act id was replayed with a different body (Agents page spec §9).</summary>
    public static readonly ErrorDefinition AgentOpBusy = new("SND-AGENT-009", ErrorKind.Conflict, "An earlier op on this row is still sending, or the act id was used with another body (fix: wait for it, or use a new act id)");

    /// <summary>SND-AGENT-010: an 'agents.*' profile key is out of range; the detail carries the key path (Agents page spec §9).</summary>
    public static readonly ErrorDefinition AgentSettingInvalid = new("SND-AGENT-010", ErrorKind.Invalid, "An agents profile key is not valid");

    /// <summary>SND-AGENT-011: a recycle job ended without a new session inside its wait (AW-META-022; a job outcome, Agents page spec §9).</summary>
    public static readonly ErrorDefinition AgentRecycleTimedOut = new("SND-AGENT-011", ErrorKind.Timeout, "The recycle ended without a new session (fix: check the lane's terminal and recycle again)");

    /// <summary>The Agents codes (Agents page spec §9), for <see cref="All"/>.</summary>
    private static ErrorDefinition[] EveryAgent() =>
        [AgentRowNotFound, AgentVersionStale, AgentOpNotApplicable, AgentStopRefused, AgentRecycleRefused, AgentMetaUnavailable, AgentMessageInvalid, AgentActivityUnreadable, AgentOpBusy, AgentSettingInvalid, AgentRecycleTimedOut];
}
