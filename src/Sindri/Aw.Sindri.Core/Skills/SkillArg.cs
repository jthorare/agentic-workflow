namespace Aw.Sindri.Core.Skills;

/// <summary>One item of a block's 'args' list (skill flows spec §3.1 "args grammar"): literal text with '{input}' slots, or a flag fragment that is dropped whole when its optional input is empty.</summary>
/// <param name="Text">The literal text with '{input}' slots; null for a fragment.</param>
/// <param name="Flag">The fragment's flag; null for a text item.</param>
/// <param name="Input">The fragment's input key; null for a text item.</param>
public sealed record SkillArg(string? Text, string? Flag = null, string? Input = null);
