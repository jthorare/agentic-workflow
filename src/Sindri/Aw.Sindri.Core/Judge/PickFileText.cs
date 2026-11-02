namespace Aw.Sindri.Core.Judge;

/// <summary>One file of the pack a model-pick question is shown.</summary>
/// <param name="Path">The file's path.</param>
/// <param name="Text">Its text.</param>
public sealed record PickFileText(string Path, string Text);
