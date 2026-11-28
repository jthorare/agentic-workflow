using System.Globalization;

namespace Aw.Context.Core.Linking;

public static partial class RulesLinker
{
    // why: a plain scan, not a regex. NonBacktracking `[\p{L}\p{N}]+` met q93 (one pass, no clock) but paid ~2 µs a char the first time it saw a longer
    // text than before: 2 s on one ~1 MB record, 20-30 s on a loaded CI runner. This is the same one pass, no clock, and no engine to warm.
    // A UTF-16 char at a time, like the regex was: a surrogate half is no letter, so a character outside the BMP splits a word exactly as before.
    internal static IEnumerable<string> LowerWords(string text)
    {
        var start = -1;
        for (var i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && IsLetterOrNumber(text[i]))
            {
                start = start < 0 ? i : start;
            }
            else if (start >= 0)
            {
                yield return text[start..i].ToLowerInvariant();
                start = -1;
            }
        }
    }

    private static bool IsLetterOrNumber(char c) => CharUnicodeInfo.GetUnicodeCategory(c) is
        UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter
        or UnicodeCategory.DecimalDigitNumber or UnicodeCategory.LetterNumber or UnicodeCategory.OtherNumber;
}
