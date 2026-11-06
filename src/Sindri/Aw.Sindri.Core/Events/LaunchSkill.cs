namespace Aw.Sindri.Core.Events;

/// <summary>The skill block a launch ran, on 'launch.started' (skill flows spec §7 Ledger): which skill, the descriptor version and manifest it loaded, the SKILL.md it ran, whether it may push, and whether it was a trial.</summary>
/// <param name="Name">The skill's folder name.</param>
/// <param name="Version">The descriptor version.</param>
/// <param name="Manifest">The block.yaml path; null when unknown.</param>
/// <param name="SkillMdSha">The SHA-256 of the SKILL.md; null when unknown.</param>
/// <param name="Push">True when the block asked to push (a trial never pushes).</param>
/// <param name="Trial">True for 'sindri skill run'.</param>
public sealed record LaunchSkill(string Name, int Version, string? Manifest, string? SkillMdSha, bool Push, bool Trial);
