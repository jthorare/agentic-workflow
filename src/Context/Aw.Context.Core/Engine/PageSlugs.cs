using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Aw.Context.Core.Engine;

/// <summary>The ids pages and actions get when the profile leaves them out (pages spec §3.1): the name lower-cased, words joined by '-'.</summary>
public static class PageSlugs
{
    private const string Fallback = "page";
    private const int MaxLength = 64;
    private const int HashLength = 8;

    /// <summary>The slug of a name: 'Schedule week' is 'schedule-week'. A name with letters or digits outside a-z/0-9 gets the first 8 hex of the SHA-256 of the name appended ('日本語' is 'page-' plus the hash), so names that differ only there get different ids; a blank name gives 'page'.</summary>
    /// <param name="name">The name.</param>
    /// <returns>A slug that is a valid <see cref="PageId"/> and <see cref="PageActionId"/>.</returns>
    public static string Of(string? name)
    {
        var words = new StringBuilder();
        var pending = false;
        var text = name ?? string.Empty;
        foreach (var ch in text.ToLowerInvariant())
        {
            if (ch is (>= 'a' and <= 'z') or (>= '0' and <= '9'))
            {
                if (pending && words.Length > 0)
                {
                    _ = words.Append('-');
                }

                pending = false;
                _ = words.Append(ch);
            }
            else
            {
                pending = true;
            }
        }

        if (HasNonAsciiLetterOrDigit(text))
        {
            return WithHash(words.ToString(), text);
        }

        var slug = words.Length > MaxLength ? words.ToString(0, MaxLength).TrimEnd('-') : words.ToString();
        return slug.Length == 0 ? Fallback : slug;
    }

    private static bool HasNonAsciiLetterOrDigit(string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            if (Rune.IsLetterOrDigit(rune) && !rune.IsAscii)
            {
                return true;
            }
        }

        return false;
    }

    private static string WithHash(string words, string text)
    {
        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.Trim().Normalize(NormalizationForm.FormC).ToLowerInvariant())))[..HashLength];
        var room = MaxLength - HashLength - 1;
        var head = words.Length > room ? words[..room].TrimEnd('-') : words;
        return (head.Length == 0 ? Fallback : head) + "-" + hash;
    }

    /// <summary>The first slug not in <paramref name="taken"/>: the slug itself, else with '-2', '-3' and so on. The result is added to <paramref name="taken"/>.</summary>
    /// <param name="slug">The slug of a name.</param>
    /// <param name="taken">The slugs already given.</param>
    /// <returns>An unused slug.</returns>
    public static string Unique(string slug, ISet<string> taken)
    {
        ArgumentNullException.ThrowIfNull(slug);
        ArgumentNullException.ThrowIfNull(taken);
        var candidate = slug;
        for (var n = 2; !taken.Add(candidate); n++)
        {
            var suffix = string.Create(CultureInfo.InvariantCulture, $"-{n}");
            candidate = (slug.Length + suffix.Length > MaxLength ? slug[..(MaxLength - suffix.Length)].TrimEnd('-') : slug) + suffix;
        }

        return candidate;
    }
}
