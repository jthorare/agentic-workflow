using System.Text;

namespace Aw.Context.Core.ConceptSynthesis;

/// <summary>Makes the key a new concept is stored under.</summary>
public static class ConceptKeys
{
    private const int MaxLength = 60;

    /// <summary>A slug of the model's name (lowercase letters and digits, joined by single hyphens), de-duplicated with -2, -3 against the keys in use.</summary>
    /// <param name="name">The concept's name.</param>
    /// <param name="taken">The keys already in use.</param>
    /// <returns>A key that is not in <paramref name="taken"/>.</returns>
    public static string Slug(string name, IReadOnlySet<string> taken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(taken);
        var slug = new StringBuilder();
        foreach (var c in name.ToLowerInvariant())
        {
            if (c is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                _ = slug.Append(c);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                _ = slug.Append('-');
            }
        }

        var baseKey = slug.ToString().TrimEnd('-');
        baseKey = baseKey.Length > MaxLength ? baseKey[..MaxLength].TrimEnd('-') : baseKey;
        if (baseKey.Length == 0)
        {
            baseKey = "concept";
        }

        var key = baseKey;
        for (var n = 2; taken.Contains(key); n++)
        {
            key = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{baseKey}-{n}");
        }

        return key;
    }
}
