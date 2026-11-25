namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>What a 'memory_search' node asks <see cref="IGraphMemoryPort"/> to recall.</summary>
/// <param name="Memory">The memory's name.</param>
/// <param name="Query">What to recall.</param>
/// <param name="Level">'loose', 'balanced' or 'strict'; null is balanced.</param>
/// <param name="Limit">At most this many facts; null takes the slice's default.</param>
public sealed record MemoryToRecall(string Memory, string Query, string? Level, int? Limit);
