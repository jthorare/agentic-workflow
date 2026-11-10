using System.Globalization;
using System.Text;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>Makes a short id (a slug) from a workflow's name, by the rule Sindri holds: lowercase letters, digits and '-', a letter or digit first, at most 63.</summary>
internal static class WorkflowSlugs
{
    /// <summary>The most characters a slug holds.</summary>
    internal const int Max = 63;

    /// <summary>The short id a name gives: accents fold ("été" is "ete"), every other run of characters becomes one '-', the ends are trimmed, and a long one is cut at a word boundary.</summary>
    /// <param name="name">What the person typed.</param>
    /// <returns>The short id; empty when the name has no letter or digit a short id can use.</returns>
    internal static string From(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        var folded = new StringBuilder(name.Length);
        foreach (var c in name.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                _ = folded.Append(char.ToLowerInvariant(c));
            }
        }

        var slug = new StringBuilder(folded.Length);
        foreach (var c in folded.ToString())
        {
            _ = slug.Append(IsSlugChar(c) ? c : '-');
        }

        var joined = string.Join('-', slug.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
        return Cut(joined);
    }

    /// <summary>True for a letter a slug may start with: a to z or 0 to 9.</summary>
    /// <param name="c">The character.</param>
    /// <returns>True when it is one.</returns>
    internal static bool IsSlugStart(char c) => c is (>= 'a' and <= 'z') or (>= '0' and <= '9');

    private static bool IsSlugChar(char c) => IsSlugStart(c);

    // A long id is cut at the last '-' at or before the 63rd character, so it ends on a whole word, and never ends in '-'.
    private static string Cut(string slug)
    {
        if (slug.Length <= Max)
        {
            return slug;
        }

        var dash = slug[..(Max + 1)].LastIndexOf('-');
        return (dash > 0 ? slug[..dash] : slug[..Max]).TrimEnd('-');
    }
}
