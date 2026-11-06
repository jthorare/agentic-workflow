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

    /// <summary>The descriptor version the block loaded at ('launch.started' records it); 0 when unknown.</summary>
    public int DescriptorVersion { get; init; }

    /// <summary>The block.yaml the block was read from; null when unknown.</summary>
    public string? ManifestPath { get; init; }

    /// <summary>The SHA-256 of the SKILL.md the block was loaded with (<see cref="LoadedSkillBlock.SkillMdSha"/>); null when unknown.</summary>
    public string? SkillMdSha { get; init; }

    /// <summary>True for 'sindri skill run': the launch is a trial, scrubbed whatever the block asked for, and 'launch.started' says so.</summary>
    public bool Trial { get; init; }
}
