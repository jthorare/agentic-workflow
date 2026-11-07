namespace Aw.Sindri.Core.Skills;

/// <summary>What a skill launch checks before any session starts: the agent home the launch uses (skill flows spec §3.1 requires.yaml check, SND-SKILL-005/007).</summary>
public interface IAgentHome
{
    /// <summary>The agent home folder ('~/.claude', the separate login's CLAUDE_CONFIG_DIR, '~/.codex').</summary>
    public string Folder { get; }

    /// <summary>Whether <paramref name="bin"/> is on the launch's PATH.</summary>
    /// <param name="bin">The executable's name.</param>
    /// <returns>True when found.</returns>
    public bool HasBin(string bin);

    /// <summary>Whether the agent home carries the agent type.</summary>
    /// <param name="agent">The agent type's name.</param>
    /// <returns>True when found.</returns>
    public bool HasAgent(string agent);

    /// <summary>Whether the MCP server is registered with the agent this home belongs to.</summary>
    /// <param name="server">The server's name.</param>
    /// <returns>True when registered.</returns>
    public bool HasMcp(string server);

    /// <summary>Whether the agent home links the skill (its folder holds a SKILL.md).</summary>
    /// <param name="skill">The skill's folder name.</param>
    /// <returns>True when found.</returns>
    public bool HasSkill(string skill);

    /// <summary>The skill's SKILL.md in this home, symlinks followed, with the SHA-256 of its bytes.</summary>
    /// <param name="skill">The skill's folder name.</param>
    /// <returns>The resolved path and the sha; the sha is null when the file is missing or unreadable.</returns>
    public (string Path, string? Sha) SkillMarkdown(string skill);
}
