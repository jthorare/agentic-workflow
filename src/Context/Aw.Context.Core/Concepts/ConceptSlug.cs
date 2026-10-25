using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Aw.Context.Core.Concepts;

/// <summary>Concept ids: lower-case ASCII words joined by '-' or '.', at most 64 characters, unique within one derivation (the shape <see cref="Engine.ConceptId"/> accepts).</summary>
internal static class ConceptSlug
{
    private const int MaxLength = 64;

    internal static string Unique(string text, HashSet<string> taken)
    {
        var slug = Of(text);
        var candidate = slug;
        for (var n = 2; !taken.Add(candidate); n++)
        {
            var suffix = string.Create(CultureInfo.InvariantCulture, $"-{n}");
            candidate = slug[..Math.Min(slug.Length, MaxLength - suffix.Length)].TrimEnd('-', '.') + suffix;
        }

        return candidate;
    }

    // Accents are dropped (e-acute becomes e); any other character outside [a-z0-9.] becomes '-'. A text with nothing left (all non-Latin, say) gets a short stable hash of itself.
    private static string Of(string text)
    {
        var slug = new StringBuilder();
        foreach (var c in text.Trim().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(c);
            if (lower is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '.')
            {
                _ = slug.Append(lower);
            }
            else if (slug.Length > 0 && slug[^1] != '-')
            {
                _ = slug.Append('-');
            }
        }

        var cleaned = slug.ToString().Trim('-', '.');
        cleaned = cleaned.Length > MaxLength ? cleaned[..MaxLength].TrimEnd('-', '.') : cleaned;
        return cleaned.Length == 0 ? Hashed(text) : cleaned;
    }

    private static string Hashed(string text) =>
        "concept-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)).AsSpan(0, 4));
}
