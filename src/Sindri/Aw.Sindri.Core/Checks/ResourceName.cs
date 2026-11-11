namespace Aw.Sindri.Core.Checks;

/// <summary>Tells a lowercase, dash-separated resource name (a k8s Secret name, an ESO reference) from a random credential (#2644).</summary>
public static class ResourceName
{
    private const int MaxTrailingDigits = 3;

    /// <summary>True when the value is a name, not a credential: lowercase words joined by at least one dash, where digits only end a segment (v2, env1).</summary>
    /// <param name="value">The value an assignment holds.</param>
    /// <returns>True for a name shape.</returns>
    public static bool Looks(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var segments = value.Split('-');
        return segments.Length >= 2 && segments.All(SegmentLooksLikeWord) && char.IsAsciiLetterLower(value[0]);
    }

    /// <summary>True when the key's secret word is SECRET (ESO_SECRET, db-secret): such a key may hold the name of a k8s Secret. PASSWORD, TOKEN, API_KEY, ACCESS_KEY, PRIVATE_KEY and SECRET_KEY keys hold the credential itself, so they are never exempt.</summary>
    /// <param name="key">The name the value is assigned to.</param>
    /// <returns>True for a key ending in SECRET, in any case.</returns>
    public static bool KeyNamesASecret(string key)
    {
        ArgumentNullException.ThrowIfNull(key);
        return key.EndsWith("SECRET", StringComparison.OrdinalIgnoreCase);
    }

    private static bool SegmentLooksLikeWord(string segment)
    {
        var letters = 0;
        while (letters < segment.Length && char.IsAsciiLetterLower(segment[letters]))
        {
            letters++;
        }

        var digits = segment.Length - letters;
        if (digits > MaxTrailingDigits || letters + digits != segment.Length)
        {
            return false;
        }

        for (var i = letters; i < segment.Length; i++)
        {
            if (!char.IsAsciiDigit(segment[i]))
            {
                return false;
            }
        }

        return letters > 0;
    }
}
