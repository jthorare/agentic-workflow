using System.Text;
using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>The checks the Pin sheet makes on a word as it is filled in or typed (concept pins spec §3.4, §3.6): its shape, and whether another pin already owns it. The server checks again at the write.</summary>
internal static class PinWordRules
{
    private const int MaxWord = 60;

    // Keep in step with AliasRules._smallWords in Aw.Sindri.Core (this project can't reference it).
    private static readonly HashSet<string> _smallWords = new(StringComparer.Ordinal)
    {
        "a", "an", "and", "are", "as", "at", "be", "by", "can", "do", "does", "done", "for", "from", "how", "i", "if", "in", "is", "it", "its", "of", "on",
        "or", "s", "the", "this", "that", "to", "was", "what", "when", "where", "which", "who", "why", "with",
    };

    /// <summary>Why a word can't be an alias of the pin <paramref name="self"/>, or null when it can.</summary>
    /// <param name="word">The word.</param>
    /// <param name="pins">Every pin of the live profile.</param>
    /// <param name="self">The id of the pin being filled in (it can repeat its own words); null for a new one.</param>
    /// <returns>The sentence the row shows, or null.</returns>
    internal static string? Refusal(string word, IReadOnlyList<PinView> pins, string? self)
    {
        ArgumentNullException.ThrowIfNull(word);
        ArgumentNullException.ThrowIfNull(pins);
        if (word.Length is 0 or > MaxWord || !word.Any(char.IsLetterOrDigit) || word.Any(char.IsControl) || Normalized(word).Length == 0)
        {
            return Labels.PinSheetWordShape;
        }

        var form = Normalized(word);
        foreach (var pin in pins.Where(p => !string.Equals(p.Id, self, StringComparison.Ordinal)))
        {
            var taken = new[] { pin.Id, pin.Name }.Concat(pin.Aliases).FirstOrDefault(t => string.Equals(Normalized(t), form, StringComparison.Ordinal));
            if (taken is not null)
            {
                return Labels.PinSheetWordTaken(pin.Name, taken);
            }
        }

        return null;
    }

    /// <summary>The word in the matcher's form: lower-case letter and digit runs, small words left out, a plural 's' dropped, sorted.</summary>
    internal static string Normalized(string text)
    {
        var words = new SortedSet<string>(StringComparer.Ordinal);
        var word = new StringBuilder();
        foreach (var c in text + " ")
        {
            if (char.IsLetterOrDigit(c))
            {
                _ = word.Append(char.ToLowerInvariant(c));
                continue;
            }

            if (word.Length > 0)
            {
                var w = word.ToString();
                _ = word.Clear();
                if (!_smallWords.Contains(w) && !_smallWords.Contains(w = Singular(w)))
                {
                    _ = words.Add(w);
                }
            }
        }

        return string.Join(' ', words);
    }

    private static string Singular(string word) =>
        word.Length > 3 && word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal) ? word[..^1] : word;
}
