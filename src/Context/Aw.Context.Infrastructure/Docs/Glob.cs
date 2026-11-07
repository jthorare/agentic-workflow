using System.Text;
using System.Text.RegularExpressions;

namespace Aw.Context.Infrastructure.Docs;

/// <summary>
/// The doc globs of 'context.docs.globs', matched against '/'-separated repo-relative paths: '**/' is any number of
/// folders (none included), '**' anything, '*' anything within one folder. The settings reader admits only letters,
/// digits and '._-/*', so nothing else needs escaping beyond '.'. The library's globs ignore case (a vault on a case-insensitive disk has 'Secrets/').
/// </summary>
internal sealed class Glob(IReadOnlyList<string> globs, bool ignoreCase = false)
{
    private readonly Regex[] _patterns = [.. globs.Select(g => new Regex(ToRegex(g), ignoreCase ? RegexOptions.CultureInvariant | RegexOptions.IgnoreCase : RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1)))];

    /// <summary>True when any glob matches <paramref name="path"/>.</summary>
    /// <param name="path">A repo-relative path.</param>
    /// <returns>Whether it matches.</returns>
    public bool Matches(string path) => _patterns.Any(p => p.IsMatch(path));

    internal static string ToRegex(string glob)
    {
        var regex = new StringBuilder("^");
        for (var i = 0; i < glob.Length; i++)
        {
            if (glob.AsSpan(i).StartsWith("**/", StringComparison.Ordinal))
            {
                _ = regex.Append("(?:[^/]+/)*");
                i += 2;
            }
            else if (glob.AsSpan(i).StartsWith("**", StringComparison.Ordinal))
            {
                _ = regex.Append(".*");
                i++;
            }
            else
            {
                _ = regex.Append(glob[i] == '*' ? "[^/]*" : Regex.Escape(glob[i].ToString()));
            }
        }

        return regex.Append("\\z").ToString();
    }
}
