namespace Aw.Context.Core.Records;

/// <summary>Builds the FTS5 'MATCH' text of the lexical leg (spec §6.1): each term a quoted phrase, joined with OR, so user text is never read as query syntax and a long question still matches.</summary>
public static class LexicalMatch
{
    /// <summary>The most terms one match holds.</summary>
    public const int MaxTerms = 32;

    /// <summary>The longest term kept, in characters; a longer one is cut.</summary>
    public const int MaxTermLength = 256;

    /// <summary>The match text for a query's terms.</summary>
    /// <param name="terms">Plain terms.</param>
    /// <returns>'"a" OR "b"', or null when no term has a letter or digit (nothing to search for).</returns>
    public static string? Of(IReadOnlyList<string> terms)
    {
        ArgumentNullException.ThrowIfNull(terms);
        var phrases = terms
            .Select(Clean)
            .Where(t => t.Any(char.IsLetterOrDigit))
            .Distinct(StringComparer.Ordinal)
            .Take(MaxTerms)
            .Select(t => "\"" + t.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"")
            .ToList();
        return phrases.Count == 0 ? null : string.Join(" OR ", phrases);
    }

    private static string Clean(string term)
    {
        var text = new string([.. term.Select(c => char.IsControl(c) ? ' ' : c)]).Trim();
        return text.Length > MaxTermLength ? text[..MaxTermLength].TrimEnd() : text;
    }
}
