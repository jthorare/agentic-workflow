using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Tests.Sessions;

/// <summary>Small builders for the session rule tests.</summary>
internal static class SessionData
{
    public static readonly DateTimeOffset T0 = new(2026, 10, 16, 12, 0, 0, TimeSpan.Zero);

    public static AgentEntry Claude { get; } = new(AgentId.From("claude-code"), "Claude Code", "claude", ProcessName: "claude", Transcripts: "~/.claude/projects/*/{sessionId}.jsonl", Resume: ["{bin}", "--resume", "{sessionId}"]);

    public static AgentEntry Codex { get; } = new(AgentId.From("codex"), "Codex", "codex", ProcessName: "codex", Transcripts: null, Resume: null);

    public static AgentEntry Cursor { get; } = new(AgentId.From("cursor"), "Cursor", "agent", ProcessName: "agent", Transcripts: null, Resume: null);

    public static AgentEntry Aider { get; } = new(AgentId.From("aider"), "Aider", "aider", ProcessName: "aider", Transcripts: null, Resume: null);

    public static IReadOnlyList<AgentEntry> Agents { get; } = [Claude, Codex, Cursor, Aider];

    public static ProcessEntry Proc(int pid, int parent, string comm, string? argv0 = null, string? exe = null, int tty = 34816, long ticks = 100) =>
        new(pid, parent, comm, tty, ticks) { Argv0 = argv0, ExePath = exe };

    public static ObservedSession Session(string provider, string id, SessionState state = SessionState.Running, DateTimeOffset? since = null, int pid = 10, long ticks = 100) =>
        new(new SessionRef(AgentId.From(provider), SessionId.From(id)), pid, ticks, state, StateSource.Record, since ?? T0) { IdKnown = true };

    public static ClaudeRecord Record(string? status, DateTimeOffset? updated, int pid = 10) =>
        new(pid, "100") { Status = status, StatusUpdatedAt = updated };

    public static FleetStateFile Hook(HookState state, DateTimeOffset at, AskingReason? reason = null, string? tool = null) =>
        new(AgentId.From("claude-code"), SessionId.From("s1"), state, at) { Reason = reason, Tool = tool };
}
