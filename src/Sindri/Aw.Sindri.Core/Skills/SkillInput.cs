namespace Aw.Sindri.Core.Skills;

/// <summary>One input of a skill block (skill flows spec §3.1).</summary>
/// <param name="Key">The input's name; also the 'with:' parameter and the '{key}' slot in the args.</param>
/// <param name="Type">Text or path; picks the form field.</param>
/// <param name="Label">The label the form shows.</param>
/// <param name="Required">Whether the recipe must give a value.</param>
/// <param name="Values">Null, or the values a text input may take.</param>
public sealed record SkillInput(string Key, SkillValueType Type, string Label, bool Required, IReadOnlyList<string>? Values = null);
