namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>What a 'jev' node sends.</summary>
/// <param name="Model">The Jev model, or null for the judge's pinned one.</param>
/// <param name="Text">What the questions are about.</param>
/// <param name="Questions">The questions.</param>
public sealed record JevCall(string? Model, string Text, IReadOnlyList<JevQuestion> Questions);
