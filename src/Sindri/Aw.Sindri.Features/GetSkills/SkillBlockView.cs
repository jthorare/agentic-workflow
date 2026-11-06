namespace Aw.Sindri.Features.GetSkills;

/// <summary>One loaded skill block as the API serves it (skill flows spec §7).</summary>
/// <param name="Id">The block id.</param>
/// <param name="Skill">The skill's folder name.</param>
/// <param name="Descriptor">The descriptor id ('step.skill.review').</param>
/// <param name="Version">The descriptor version.</param>
/// <param name="Sentence">The block's sentence.</param>
/// <param name="Inputs">The inputs.</param>
/// <param name="Outputs">The outputs, as fact keys.</param>
/// <param name="Outcomes">The outcomes.</param>
/// <param name="Pushes">Whether the block pushes.</param>
/// <param name="SkillSha">The SKILL.md sha256.</param>
/// <param name="Path">The block.yaml it was loaded from.</param>
internal sealed record SkillBlockView(string Id, string Skill, string Descriptor, int Version, string Sentence, IReadOnlyList<SkillInputView> Inputs, IReadOnlyList<string> Outputs, IReadOnlyList<string> Outcomes, bool Pushes, string SkillSha, string Path);
