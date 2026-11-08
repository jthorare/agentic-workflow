namespace Aw.Sindri.Core.Memory;

/// <summary>The answer of a recall: the facts best first, and how they were ranked.</summary>
/// <param name="Facts">The facts, best first.</param>
/// <param name="Mode">How they were ranked.</param>
public sealed record MemoryRecall(IReadOnlyList<RecalledFact> Facts, RecallMode Mode)
{
    /// <summary>Ranks facts by cosine similarity to the query vector. Facts with no embedding are skipped.</summary>
    /// <param name="query">The query's vector.</param>
    /// <param name="facts">The unexpired facts of the memory.</param>
    /// <param name="level">The floor: facts below <see cref="MatchLevels.Threshold"/> are left out.</param>
    /// <param name="limit">At most this many facts.</param>
    /// <returns>The recall, mode semantic.</returns>
    public static MemoryRecall Semantic(ReadOnlyMemory<float> query, IReadOnlyList<MemoryFact> facts, MatchLevel level, int limit)
    {
        ArgumentNullException.ThrowIfNull(facts);
        var floor = MatchLevels.Threshold(level);
        var ranked = facts
            .Where(static f => !f.Embedding.IsEmpty)
            .Select(f => new RecalledFact(f, MemoryDedupe.Similarity(query, f.Embedding)))
            .Where(r => r.Score >= floor)
            .OrderByDescending(static r => r.Score)
            .Take(limit)
            .ToList();
        return new MemoryRecall(ranked, RecallMode.Semantic);
    }
}
