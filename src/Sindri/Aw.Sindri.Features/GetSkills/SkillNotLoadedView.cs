namespace Aw.Sindri.Features.GetSkills;

/// <summary>A block that did not load and why.</summary>
/// <param name="Id">The block id, or the skill's folder name.</param>
/// <param name="Path">The block.yaml.</param>
/// <param name="Reason">The reason.</param>
internal sealed record SkillNotLoadedView(string Id, string Path, string Reason);
