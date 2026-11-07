namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>One output of a skill block, as a path.</summary>
/// <param name="Label">The fact's label.</param>
/// <param name="Path">The path the fact holds.</param>
internal sealed record SkillOutput(string Label, string Path);
