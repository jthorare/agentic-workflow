namespace Aw.Sindri.Testing.Workflow;

/// <summary>One skill a <see cref="SkillCatalogPortContract"/> test seeds.</summary>
/// <param name="Name">The skill's folder name.</param>
/// <param name="Description">Its SKILL.md description, or null for a SKILL.md with none.</param>
public sealed record SkillCatalogSeed(string Name, string? Description);
