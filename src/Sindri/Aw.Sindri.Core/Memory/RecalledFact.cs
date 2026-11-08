namespace Aw.Sindri.Core.Memory;

/// <summary>A fact and its score for one query.</summary>
/// <param name="Fact">The fact.</param>
/// <param name="Score">Cosine similarity (semantic) or the share of query tokens found (keyword), from 0 to 1.</param>
public sealed record RecalledFact(MemoryFact Fact, float Score);
