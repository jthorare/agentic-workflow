namespace Aw.Sindri.Core.Memory;

/// <summary>The fallback ranker when no embedding model is available: token overlap, no model.</summary>
public static class KeywordRank
{
    /// <summary>Ranks facts by the share of the query's tokens (lowercase words of 2 or more letters or digits) their text or tags contain.</summary>
    /// <param name="query">The query.</param>
    /// <param name="facts">The unexpired facts of the memory.</param>
    /// <param name="limit">At most this many facts.</param>
    /// <returns>The recall, mode keyword; facts sharing no token are left out.</returns>
    public static MemoryRecall Rank(string query, IReadOnlyList<MemoryFact> facts, int limit)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(facts);
        var wanted = Tokens(query);
        var ranked = wanted.Count == 0
            ? []
            : facts
                .Select(f => new RecalledFact(f, (float)Tokens(string.Join(' ', [f.Text, .. f.Tags])).Count(wanted.Contains) / wanted.Count))
                .Where(static r => r.Score > 0f)
                .OrderByDescending(static r => r.Score)
                .Take(limit)
                .ToList();
        return new MemoryRecall(ranked, RecallMode.Keyword);
    }

    private static HashSet<string> Tokens(string text)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        var word = new System.Text.StringBuilder();
        foreach (var c in text.ToLowerInvariant().Append(' '))
        {
            if (char.IsLetterOrDigit(c))
            {
                _ = word.Append(c);
                continue;
            }

            if (word.Length >= 2)
            {
                _ = tokens.Add(word.ToString());
            }

            _ = word.Clear();
        }

        return tokens;
    }
}
