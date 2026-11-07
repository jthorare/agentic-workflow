namespace Aw.Sindri.Core.Skills;

/// <summary>Whose form the skill's invocation takes (skill flows spec D2): '/review 42' for Claude Code, '$review 42' for Codex.</summary>
public enum SkillTarget
{
    /// <summary>Claude Code: a slash command.</summary>
    ClaudeCode = 0,

    /// <summary>Codex: a dollar skill.</summary>
    Codex = 1,
}
