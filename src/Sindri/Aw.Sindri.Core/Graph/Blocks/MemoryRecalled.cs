namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>What <see cref="IGraphMemoryPort.Recall"/> found.</summary>
/// <param name="Facts">The fact texts, best first.</param>
/// <param name="Mode">'semantic', or 'keyword' when the embedding model did not answer.</param>
public sealed record MemoryRecalled(IReadOnlyList<string> Facts, string Mode);
