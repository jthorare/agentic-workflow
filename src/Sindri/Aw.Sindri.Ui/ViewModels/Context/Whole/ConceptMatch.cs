using System.Text;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>Picks the concept a page's name points at: the one that shares the most words with it (the server's words: case and plural ignored, no stemming); a tie goes to the concept with more pages, then to the first.</summary>
internal static class ConceptMatch
{
    private const int MaxWordLength = 64;

    private static readonly HashSet<string> _small = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "can", "do", "does", "done", "for", "from", "how", "i", "if", "in", "is", "it", "its", "of", "on",
        "or", "s", "the", "this", "that", "to", "was", "what", "when", "where", "which", "who", "why", "with",
    };

    /// <summary>Finds the concept a page name points at.</summary>
    /// <param name="pageName">The page's name.</param>
    /// <param name="known">The concepts, in the Context page's order.</param>
    /// <returns>The concept, or null when no word is shared.</returns>
    internal static ConceptChoice? Best(string pageName, IReadOnlyList<ConceptChoice> known)
    {
        ArgumentNullException.ThrowIfNull(known);
        var words = Words(pageName);
        return known.Select(c => (Concept: c, Shared: Words(c.Name).Intersect(words, StringComparer.Ordinal).Count()))
            .Where(static m => m.Shared > 0)
            .OrderByDescending(static m => m.Shared)
            .ThenByDescending(static m => m.Concept.Pages)
            .Select(static m => m.Concept)
            .FirstOrDefault();
    }

    // A client copy of the server's word rule, so a name picks the same concept here as in a paste: keep in step with AnchorMatch.Words (src/Context/Aw.Context.Core/Graph/AnchorMatch.cs)
    // and the QueryRules.Words and _small list it uses (the UI cannot reference Aw.Context.Core). Letter and digit runs, lower-cased, 64 characters at most; small words dropped before and
    // after a plural 's' is dropped.
    private static HashSet<string> Words(string text)
    {
        var words = new List<string>();
        var word = new StringBuilder();
        foreach (var c in text.Append(' '))
        {
            if (char.IsLetterOrDigit(c) && word.Length < MaxWordLength)
            {
                _ = word.Append(char.ToLowerInvariant(c));
            }
            else if (!char.IsLetterOrDigit(c) && word.Length > 0)
            {
                words.Add(word.ToString());
                _ = word.Clear();
            }
        }

        return [.. words.Where(static w => !_small.Contains(w)).Select(Singular).Where(static w => !_small.Contains(w))];
    }

    private static string Singular(string word) =>
        word.Length > 3 && word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal) ? word[..^1] : word;
}
