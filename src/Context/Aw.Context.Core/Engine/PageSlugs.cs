using System.Globalization;
using System.Text;

namespace Aw.Context.Core.Engine;

/// <summary>The ids pages and actions get when the profile leaves them out (pages spec §3.1): the name lower-cased, words joined by '-'.</summary>
public static class PageSlugs
{
    private const string Fallback = "page";
    private const int MaxLength = 64;

    /// <summary>The slug of a name: 'Schedule week' is 'schedule-week'. A name with no letter or digit gives 'page'.</summary>
    /// <param name="name">The name.</param>
    /// <returns>A slug that is a valid <see cref="PageId"/> and <see cref="PageActionId"/>.</returns>
    public static string Of(string? name)
    {
        var words = new StringBuilder();
        var pending = false;
        foreach (var ch in (name ?? string.Empty).ToLowerInvariant())
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

        var slug = words.Length > MaxLength ? words.ToString(0, MaxLength).TrimEnd('-') : words.ToString();
        return slug.Length == 0 ? Fallback : slug;
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
