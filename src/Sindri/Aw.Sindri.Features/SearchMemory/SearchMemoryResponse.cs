using Aw.Sindri.Core.Memory;

namespace Aw.Sindri.Features.SearchMemory;

/// <summary>The recalled facts, best first, each with its score and provenance.</summary>
/// <param name="Facts">The facts.</param>
/// <param name="Count">How many.</param>
/// <param name="Mode">'semantic', or 'keyword' when the embedding model did not answer.</param>
/// <param name="Note">Why the mode is keyword (SND-MEM-004's message), or null.</param>
public sealed record SearchMemoryResponse(IReadOnlyList<MemoryFactView> Facts, int Count, string Mode, string? Note);
