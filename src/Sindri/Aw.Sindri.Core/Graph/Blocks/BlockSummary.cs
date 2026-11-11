namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>The one line the canvas shows on a node, and what is missing from it.</summary>
/// <param name="Text">The line.</param>
/// <param name="Problem">What is not set yet, in plain words, or null.</param>
public sealed record BlockSummary(string Text, string? Problem = null);
