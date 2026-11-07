namespace Aw.Sindri.Core.Skills;

/// <summary>A block that did not load, and why (the doctor row 'skills:block &lt;id&gt; is not loaded: &lt;reason&gt;').</summary>
/// <param name="Name">The block id, or the skill's folder name when the file didn't parse far enough to name one.</param>
/// <param name="Path">The block.yaml.</param>
/// <param name="Reason">The reason in plain words.</param>
public sealed record SkippedSkillBlock(string Name, string Path, string Reason);
