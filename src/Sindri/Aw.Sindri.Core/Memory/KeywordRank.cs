namespace Aw.Sindri.Core.Memory;

/// <summary>The fallback ranker when no embedding model is available: token overlap, no model.</summary>
public static class KeywordRank
{
    /// <summary>Ranks facts by the share of the query's tokens (lowercase words of 2 or more letters or digits) their text or tags contain.</summary>
    /// <param name="query">The query.</param>
    /// <param name="facts">The unexpired facts of the memory.</param>
    /// <param name="limit">At most this many facts.</param>
    /// <returns>The recall, mode keyword; facts sharing no token are left out.</returns>
    public static MemoryRecall Rank(string query, IReadOnlyList<MemoryFact> facts, int limit) => throw new NotSupportedException();
}
