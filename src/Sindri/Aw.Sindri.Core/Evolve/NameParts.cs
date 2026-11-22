namespace Aw.Sindri.Core.Evolve;

/// <summary>Splits a name into its parts at case and letter-digit boundaries ('AcmeCare', 'GLXCare', 'acme2'), lowercased.</summary>
internal static class NameParts
{
    /// <summary>The parts of one word of letters and digits.</summary>
    /// <param name="word">The word as written.</param>
    /// <returns>The lowercased parts, in order.</returns>
    public static IReadOnlyList<string> Split(string word)
    {
        ArgumentNullException.ThrowIfNull(word);
        var parts = new List<string>();
        var start = 0;
        for (var i = 1; i < word.Length; i++)
        {
            if (IsBoundary(word, i))
            {
                parts.Add(word[start..i].ToLowerInvariant());
                start = i;
            }
        }

        if (start < word.Length)
        {
            parts.Add(word[start..].ToLowerInvariant());
        }

        return parts;
    }

    // why: a boundary sits before 'i' when a lower-case letter or digit meets a capital (acmeCare, v2Beta), when an upper-case run
    // ends before a capitalized word (GLXCare: before the C), or when a letter meets a digit either way (acme2, 2acme)
    private static bool IsBoundary(string word, int i)
    {
        char before = word[i - 1], at = word[i];
        if (char.IsLetter(before) != char.IsLetter(at))
        {
            return true;
        }

        if (!char.IsUpper(at))
        {
            return false;
        }

        return char.IsLower(before) || (char.IsUpper(before) && i + 1 < word.Length && char.IsLower(word[i + 1]));
    }
}
