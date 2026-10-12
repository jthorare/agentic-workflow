using System.Globalization;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>
/// The token-count grammar (spec §3.3): a whole number of at least 1, or one with a 'k' (thousands) or 'M'
/// (millions) suffix. The long form holds the number, so '200k' and 200000 hash the same.
/// </summary>
public static class TokenCounts
{
    /// <summary>Reads '1500', '200k' or '2M'.</summary>
    /// <param name="text">The text as written in the recipe.</param>
    /// <param name="count">The count, when the text is one.</param>
    /// <returns>Whether the text is a token count of at least 1 that fits an int.</returns>
    public static bool TryParse(string text, out int count)
    {
        count = 0;
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var (digits, scale) = text[^1] switch
        {
            'k' => (text[..^1], 1_000L),
            'M' => (text[..^1], 1_000_000L),
            _ => (text, 1L),
        };
        if (!long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n < 1 || n > int.MaxValue / scale)
        {
            return false;
        }

        count = (int)(n * scale);
        return true;
    }
}
