using System.Text.RegularExpressions;

namespace Aw.Sindri.Core.Evolve;

/// <summary>
/// Names from private repos (privacy spec §5.4). The set side keeps each name whole and joined ('nova-ledger', 'novaledger'), at least
/// 5 characters and not a common word. The candidate side also checks every part and every run of 2 to 8 parts joined, across
/// separators, case and digit boundaries, so 'NovaLedgerClient' and 'nova_ledger.ts' hold for 'nova-ledger' and 'SupernovaLedger' doesn't.
/// </summary>
internal static partial class IdentifiersLayer
{
    private const int MinLength = 5;
    private const int MaxRun = 8;

    /// <summary>Whether the text names any private name.</summary>
    /// <param name="text">The candidate text.</param>
    /// <param name="names">The private names.</param>
    /// <param name="commonWords">Common words, lowercased, left out of the set.</param>
    /// <returns>True on a hit.</returns>
    public static bool Hits(string text, IReadOnlyCollection<string> names, IReadOnlySet<string> commonWords)
    {
        ArgumentNullException.ThrowIfNull(names);
        ArgumentNullException.ThrowIfNull(commonWords);
        try
        {
            var set = PrivateSet(names, commonWords);
            return set.Count > 0 && Chunks(text).SelectMany(Forms).Any(set.Contains);
        }
        catch (RegexMatchTimeoutException)
        {
            return true;   // why: text that can't be read in time can't be shown clean
        }
    }

    private static HashSet<string> PrivateSet(IReadOnlyCollection<string> names, IReadOnlySet<string> commonWords)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chunk in names.SelectMany(Chunks))
        {
            foreach (var form in new[] { chunk.ToLowerInvariant(), Joined(chunk) })
            {
                if (form.Length >= MinLength && !commonWords.Contains(form))
                {
                    _ = set.Add(form);
                }
            }
        }

        return set;
    }

    // why: one name spelled with separators is one chunk: identifier characters joined by any dash, the minus sign, '.', '_', the middle dot or the hyphenation point
    private static IEnumerable<string> Chunks(string text) => ChunkPattern.Matches(PrivacyText.Visible(text)).Select(static m => m.Value);

    private static string Joined(string chunk) => string.Concat(chunk.Where(char.IsLetterOrDigit)).ToLowerInvariant();

    private static IEnumerable<string> Forms(string chunk)
    {
        yield return chunk.ToLowerInvariant();
        yield return Joined(chunk);
        var parts = PrivacyText.CasedWords(chunk).SelectMany(NameParts.Split).ToList();
        for (var start = 0; start < parts.Count; start++)
        {
            for (var length = 1; length <= MaxRun && start + length <= parts.Count; length++)
            {
                yield return string.Concat(parts.Skip(start).Take(length));
            }
        }
    }

    [GeneratedRegex(@"[\p{L}\p{N}_$]+(?:[\p{Pd}\u2212._\u00B7\u2027]+[\p{L}\p{N}_$]+)*", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ChunkPattern { get; }
}
