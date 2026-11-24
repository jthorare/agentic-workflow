namespace Aw.Sindri.Core.Evolve;

/// <summary>
/// Text copied from the turns the model saw (privacy spec §5.5): any run of 8 words shared with one session (its turns joined in order,
/// sessions never joined to each other), or a whole turn of 4 to 7 words found as a run in the candidate.
/// </summary>
internal static class VerbatimLayer
{
    private const int Shingle = 8;
    private const int ShortTurnMin = 4;

    /// <summary>Whether the text copies from any source.</summary>
    /// <param name="text">The candidate text.</param>
    /// <param name="sources">The turns the model saw, by session.</param>
    /// <returns>True on a hit.</returns>
    public static bool Hits(string text, IReadOnlyList<LeakSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);
        var words = PrivacyText.Words(text);
        var shingles = Shingles(words).ToHashSet(StringComparer.Ordinal);
        foreach (var source in sources)
        {
            if (Shingles(PrivacyText.Words(string.Join(' ', source.Turns))).Any(shingles.Contains))
            {
                return true;
            }

            if (source.Turns.Select(PrivacyText.Words).Any(t => t.Count is >= ShortTurnMin and < Shingle && TermsLayer.ContainsRun(words, t)))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> Shingles(IReadOnlyList<string> words)
    {
        for (var start = 0; start + Shingle <= words.Count; start++)
        {
            yield return string.Join(' ', words.Skip(start).Take(Shingle));
        }
    }
}
