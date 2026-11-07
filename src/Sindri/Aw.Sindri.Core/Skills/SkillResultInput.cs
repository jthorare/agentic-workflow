namespace Aw.Sindri.Core.Skills;

/// <summary>What the reader looks at: the file (null when absent), the launch, the last turn end it handled, the block, and how to tell a path exists.</summary>
/// <param name="File">The result file, or null when there is none.</param>
/// <param name="Skill">The skill's folder name; the result's 'skill' key must match it.</param>
/// <param name="Block">The block's manifest entry.</param>
/// <param name="LaunchStarted">When the launch started; a file written before it is not this launch's.</param>
/// <param name="LastHandledTurnEnd">The turn end the reader handled last; a file not newer than it is the one it already read. Null at the first turn end.</param>
/// <param name="PathExists">Whether a path output exists on disk.</param>
public sealed record SkillResultInput(SkillResultFile? File, string Skill, SkillBlockManifest Block, DateTimeOffset LaunchStarted, DateTimeOffset? LastHandledTurnEnd, Func<string, bool> PathExists)
{
    /// <summary>When the launch's last prompt was submitted (the UserPromptSubmit that cleared the turn-end marker); null when none came. A file not newer than it raises no row: the turn can end while a background subagent still runs.</summary>
    public DateTimeOffset? LastPrompt { get; init; }
}
