namespace Aw.Sindri.Core.Memory;

/// <summary>The answer of a recall: the facts best first, and how they were ranked.</summary>
/// <param name="Facts">The facts, best first.</param>
/// <param name="Mode">How they were ranked.</param>
/// <param name="Skipped">How many facts could not be compared: no vector yet, or a vector of another dimension (another model).</param>
public sealed record MemoryRecall(IReadOnlyList<RecalledFact> Facts, RecallMode Mode, int Skipped = 0)
{
    /// <summary>Ranks facts by cosine similarity to the query vector. Facts with no vector, or one of another dimension, are skipped and counted.</summary>
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
            .Where(f => f.Embedding.Length == query.Length)
            .Select(f => new RecalledFact(f, MemoryDedupe.Similarity(query, f.Embedding)))
            .Where(r => r.Score >= floor)
            .OrderByDescending(static r => r.Score)
            .Take(limit)
            .ToList();
        return new MemoryRecall(ranked, RecallMode.Semantic, facts.Count(f => f.Embedding.Length != query.Length));
    }
}
