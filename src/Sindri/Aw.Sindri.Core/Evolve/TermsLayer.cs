namespace Aw.Sindri.Core.Evolve;

/// <summary>
/// privacy.denyTerms. A term and the text are both split into name parts (<see cref="NameParts"/>); a term holds when its parts
/// appear as a run in the text's parts, or when its joined form is a whole word of the text. So 'AcmeCare' holds 'acme care',
/// 'acme-care' and 'acmecare', and 'Acme' holds 'AcmeClient' and 'acme2' but not 'Acmeville'.
/// </summary>
internal static class TermsLayer
{
    /// <summary>Whether any term appears in the text.</summary>
    /// <param name="text">The candidate text.</param>
    /// <param name="terms">The terms.</param>
    /// <returns>True on a hit.</returns>
    public static bool Hits(string text, IReadOnlyList<string> terms)
    {
        ArgumentNullException.ThrowIfNull(terms);
        var words = PrivacyText.CasedWords(text);
        var whole = words.Select(static w => w.ToLowerInvariant()).ToHashSet(StringComparer.Ordinal);
        var parts = words.SelectMany(NameParts.Split).ToList();
        foreach (var term in terms)
        {
            var termParts = PrivacyText.CasedWords(term).SelectMany(NameParts.Split).ToList();
            if (termParts.Count > 0 && (whole.Contains(string.Concat(termParts)) || ContainsRun(parts, termParts)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Whether a list holds another as a contiguous run.</summary>
    /// <param name="haystack">The list searched.</param>
    /// <param name="run">The run.</param>
    /// <returns>True when found.</returns>
    public static bool ContainsRun(IReadOnlyList<string> haystack, IReadOnlyList<string> run)
    {
        ArgumentNullException.ThrowIfNull(haystack);
        ArgumentNullException.ThrowIfNull(run);
        for (var start = 0; start + run.Count <= haystack.Count; start++)
        {
            var at = 0;
            while (at < run.Count && string.Equals(haystack[start + at], run[at], StringComparison.Ordinal))
            {
                at++;
            }

            if (at == run.Count)
            {
                return true;
            }
        }

        return false;
    }
}
