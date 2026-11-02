namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The closed set of entries inside an agent home that a session may write (sandbox spec §3.1 item 2; spike item 2). Everything else in the owner's home stays read-only.</summary>
public static class AgentHomeWriteSet
{
    private static readonly string[] _claude =
        ["projects", "sessions", "session-env", "shell-snapshots", "file-history", "todos", "history.jsonl", "backups", "cache", "state", "mcp-needs-auth-cache.json", ".last-update-result.json", ".last-cleanup"];   // spike item 2

    // The spike also lists Codex files in the home root (sqlite databases, installation_id, models_cache.json). A directory bind of the root is refused, so an owner-copy
    // session never writes them; if Codex fails without them the answer is a CODEX_HOME on the per-launch copy, not a longer list.
    private static readonly string[] _codex = ["sessions", "log", "memories", "shell_snapshots", "cache", "tmp"];

    /// <summary>Whether a session may write <paramref name="relative"/> inside <paramref name="agentHome"/> ('.../.claude' or '.../.codex').</summary>
    /// <param name="agentHome">The agent home path.</param>
    /// <param name="relative">The path inside it; only its first segment counts.</param>
    /// <returns>True when that entry is in the write set.</returns>
    public static bool Allows(string agentHome, string relative)
    {
        ArgumentNullException.ThrowIfNull(agentHome);
        ArgumentNullException.ThrowIfNull(relative);
        var set = agentHome.EndsWith("/.claude", StringComparison.Ordinal) ? _claude : agentHome.EndsWith("/.codex", StringComparison.Ordinal) ? _codex : [];
        var first = relative.Split('/', 2)[0];
        return set.Contains(first, StringComparer.Ordinal);
    }
}
