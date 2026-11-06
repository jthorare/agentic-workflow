namespace Aw.Sindri.Core.Skills;

/// <summary>A skill block's launch: what the seed and the launch environment are made from (skill flows spec §3.3).</summary>
/// <param name="Skill">The skill's folder name.</param>
/// <param name="Target">Whose invocation form the seed takes.</param>
/// <param name="Block">The block's manifest entry.</param>
/// <param name="Inputs">The input values, by key, as the recipe gave them.</param>
public sealed record SkillLaunch(string Skill, SkillTarget Target, SkillBlockManifest Block, IReadOnlyDictionary<string, string> Inputs)
{
    /// <summary>Null, or the '--skills' folder of a trial run: the seed then reads '&lt;folder&gt;/&lt;skill&gt;/SKILL.md' by path for every target, and the agent home's skill is not compared (SND-SKILL-007).</summary>
    public string? TrialSkillsDirectory { get; init; }
}
