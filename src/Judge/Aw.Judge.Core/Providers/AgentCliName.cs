namespace Aw.Judge.Core.Providers;

/// <summary>An agent-CLI provider: one per supported host. Names and binaries in <see cref="AgentCliNames"/>.</summary>
public enum AgentCliName
{
    /// <summary>'claude-cli' (binary 'claude').</summary>
    ClaudeCli = 1,

    /// <summary>'codex-cli' (binary 'codex').</summary>
    CodexCli = 2,

    /// <summary>'cursor-cli' (binary 'cursor-agent').</summary>
    CursorCli = 3,
}
