namespace Aw.Sindri.Core.Skills;

/// <summary>One block of a skill's block.yaml (skill flows spec §3.1).</summary>
/// <param name="Name">The kebab-case block id (the id in block.yaml); the descriptor is 'step.skill.&lt;id&gt;'.</param>
/// <param name="Sentence">The plain sentence the block catalog shows.</param>
/// <param name="Inputs">The block's inputs.</param>
/// <param name="Args">The args that make up the skill's invocation after its name.</param>
/// <param name="Outputs">The outputs read from the result.</param>
/// <param name="Outcomes">The outcomes.</param>
/// <param name="Pushes">True when the block pushes branches or opens PRs.</param>
/// <param name="MaxDuration">The longest the block is expected to take; the state timeout is the real bound.</param>
public sealed record SkillBlockManifest(
    string Name,
    string Sentence,
    IReadOnlyList<SkillInput> Inputs,
    IReadOnlyList<SkillArg> Args,
    IReadOnlyList<SkillOutput> Outputs,
    SkillOutcomes Outcomes,
    bool Pushes,
    TimeSpan MaxDuration);
