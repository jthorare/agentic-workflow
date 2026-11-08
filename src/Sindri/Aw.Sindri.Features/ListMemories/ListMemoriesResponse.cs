using Aw.Sindri.Core.Memory;

namespace Aw.Sindri.Features.ListMemories;

/// <summary>The memories, sorted by name.</summary>
/// <param name="Memories">The views.</param>
public sealed record ListMemoriesResponse(IReadOnlyList<MemoryView> Memories);
