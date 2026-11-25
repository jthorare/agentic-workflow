namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>One pickable value of a block field: the value the config stores and the words the picker shows.</summary>
/// <param name="Value">What the config stores.</param>
/// <param name="Label">What the picker shows.</param>
public sealed record BlockChoice(string Value, string Label);
