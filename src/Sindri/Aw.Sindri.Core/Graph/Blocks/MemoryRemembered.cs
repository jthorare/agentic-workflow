namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>What <see cref="IGraphMemoryPort.Remember"/> did.</summary>
/// <param name="Key">The fact's id; the existing fact's when it was refreshed.</param>
/// <param name="Action">'added', or 'refreshed' when a near duplicate with the same tags was found.</param>
public sealed record MemoryRemembered(string Key, string Action);
