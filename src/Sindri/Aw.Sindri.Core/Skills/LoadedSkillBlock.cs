using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Core.Skills;

/// <summary>A skill block that loaded (skill flows spec §3.2).</summary>
/// <param name="Skill">The skill's folder name ('officeHours'), which the seed invokes.</param>
/// <param name="Manifest">The block's manifest entry.</param>
/// <param name="Descriptor">The descriptor 'step.skill.&lt;id&gt;' it registers.</param>
/// <param name="Path">The block.yaml it was read from.</param>
/// <param name="ManifestHash">The canonical hash of the manifest entry.</param>
public sealed record LoadedSkillBlock(string Skill, SkillBlockManifest Manifest, BlockDescriptor Descriptor, string Path, string ManifestHash)
{
    /// <summary>The SHA-256 (lower-case hex) of the SKILL.md file beside block.yaml, set by the loader; null when there is none. The launch compares the agent home's copy against it (SND-SKILL-007), and 'launch.started' records it.</summary>
    public string? SkillMdSha { get; init; }
}
