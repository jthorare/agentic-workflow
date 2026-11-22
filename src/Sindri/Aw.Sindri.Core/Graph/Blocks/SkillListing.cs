namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>One skill as 'skill_catalog' lists it.</summary>
/// <param name="Name">The skill's folder name under skills/, the name a 'run_agent' edits it by.</param>
/// <param name="Description">The 'description' of its SKILL.md front matter, or empty when it has none.</param>
public sealed record SkillListing(string Name, string Description);
