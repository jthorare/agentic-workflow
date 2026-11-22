using System.Globalization;
using System.Text;

namespace Aw.Sindri.Core.Evolve;

/// <summary>The one normalization every leak layer reads text through, so look-alike spellings can't slip past.</summary>
internal static class PrivacyText
{
    /// <summary>NFKC, format characters (zero-width, bidi) removed, every run of white space folded to one space, trimmed.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The visible form.</returns>
    public static string Visible(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var folded = new StringBuilder(text.Length);
        var space = false;
        foreach (var c in text.Normalize(NormalizationForm.FormKC))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format)
            {
                continue;
            }

            if (char.IsWhiteSpace(c))
            {
                space = folded.Length > 0;
                continue;
            }

            if (space)
            {
                _ = folded.Append(' ');
                space = false;
            }

            _ = folded.Append(c);
        }

        return folded.ToString();
    }

    /// <summary>The visible form lowercased and split into words of letters and digits; everything else separates.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The words, in order.</returns>
    public static IReadOnlyList<string> Words(string text) => [.. CasedWords(text).Select(static w => w.ToLowerInvariant())];

    /// <summary>The words of the visible form as written, case kept, for <see cref="NameParts"/>.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The words, in order.</returns>
    public static IReadOnlyList<string> CasedWords(string text)
    {
        var visible = Visible(text);
        var words = new List<string>();
        var start = -1;
        for (var i = 0; i <= visible.Length; i++)
        {
            var inWord = i < visible.Length && char.IsLetterOrDigit(visible[i]);
            if (inWord && start < 0)
            {
                start = i;
            }
            else if (!inWord && start >= 0)
            {
                words.Add(visible[start..i]);
                start = -1;
            }
        }

        return words;
    }
}
