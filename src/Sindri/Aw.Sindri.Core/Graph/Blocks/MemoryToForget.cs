namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>What a 'memory_forget' node asks <see cref="IGraphMemoryPort"/> to forget; exactly one of <paramref name="Fact"/> and <paramref name="Tag"/> is set.</summary>
/// <param name="Memory">The memory's name.</param>
/// <param name="Fact">One fact's id, or null.</param>
/// <param name="Tag">Every fact with this tag, or null.</param>
public sealed record MemoryToForget(string Memory, string? Fact, string? Tag);
