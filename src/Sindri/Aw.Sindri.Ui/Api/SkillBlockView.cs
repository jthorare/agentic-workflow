namespace Aw.Sindri.Ui.Api;

/// <summary>One skill block.</summary>
/// <param name="Id">The block id ('office-hours').</param>
/// <param name="Skill">The skill's name.</param>
/// <param name="Descriptor">The step descriptor a recipe uses ('step.skill.office-hours').</param>
/// <param name="Version">The block's version.</param>
/// <param name="Sentence">What the block does, in one sentence.</param>
/// <param name="Inputs">What it asks for.</param>
/// <param name="Outputs">The facts it produces ('fact:officeHoursFeatureDir').</param>
/// <param name="Outcomes">The outcome ids it can report ('needsWork').</param>
/// <param name="Pushes">True when the block can push.</param>
/// <param name="SkillSha">The skill file's hash.</param>
/// <param name="Path">Where the block's file is.</param>
internal sealed record SkillBlockView(
    string Id,
    string Skill,
    string Descriptor,
    long Version,
    string Sentence,
    IReadOnlyList<SkillInputView> Inputs,
    IReadOnlyList<string> Outputs,
    IReadOnlyList<string> Outcomes,
    bool Pushes,
    string SkillSha,
    string Path);
