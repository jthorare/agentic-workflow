namespace Aw.Sindri.Features.GetSkills;

/// <summary>One input of a served block.</summary>
/// <param name="Key">The input key.</param>
/// <param name="Type">'text' or 'path'.</param>
/// <param name="Label">The form label.</param>
/// <param name="Required">Whether it is required.</param>
internal sealed record SkillInputView(string Key, string Type, string Label, bool Required);
