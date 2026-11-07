namespace Aw.Sindri.Ui.Api;

/// <summary>One input of a skill block.</summary>
/// <param name="Key">The input key.</param>
/// <param name="Type">'text' or 'path'.</param>
/// <param name="Label">The label asked with.</param>
/// <param name="Required">True when it must be given.</param>
internal sealed record SkillInputView(string Key, string Type, string Label, bool Required);
