using System.Text;
using System.Text.RegularExpressions;

namespace Aw.Judge.Core.Traits;

/// <summary>
/// Repo-relative glob matching for a trait's 'paths': '**' spans directories, '*' and '?' stay within one path segment,
/// '{a,b}' is an alternation. An unmatched '{' or '}' is a literal character, so no glob makes an invalid pattern.
/// </summary>
public static class TraitGlob
{
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(1);

    /// <summary>Whether a trait covers a file: a trait without 'paths' applies to every changed file.</summary>
    /// <param name="paths">The trait's globs, or null.</param>
    /// <param name="file">The repo-relative path, with '/' separators.</param>
    /// <returns>True when a glob matches (or there are none).</returns>
    public static bool InScope(IReadOnlyList<string>? paths, string file) => paths is null || paths.Any(glob => Matches(glob, file));

    /// <summary>Whether one glob matches a path.</summary>
    /// <param name="glob">The glob.</param>
    /// <param name="file">The repo-relative path, with '/' separators.</param>
    /// <returns>True when the whole path matches.</returns>
    public static bool Matches(string glob, string file)
    {
        ArgumentNullException.ThrowIfNull(glob);
        ArgumentNullException.ThrowIfNull(file);
        try
        {
            return Regex.IsMatch(file, Pattern(glob), RegexOptions.CultureInvariant, _timeout);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>A '{' with no matching '}' is almost certainly a typo in a trait's 'paths'.</summary>
    /// <param name="glob">The glob.</param>
    /// <returns>True when a brace is left open.</returns>
    public static bool HasUnclosedBrace(string glob)
    {
        ArgumentNullException.ThrowIfNull(glob);
        return Braces(glob).Open.Count < glob.AsSpan().Count('{');
    }

    private static string Pattern(string glob)
    {
        var (open, close, inside) = Braces(glob);
        var pattern = new StringBuilder("^");
        for (var i = 0; i < glob.Length; i++)
        {
            var doubleStar = glob[i] == '*' && i + 1 < glob.Length && glob[i + 1] == '*';
            var slash = doubleStar && i + 2 < glob.Length && glob[i + 2] == '/';
            _ = pattern.Append(Piece(glob[i], new Spot(doubleStar, slash, open.Contains(i), close.Contains(i), inside.Contains(i))));
            i += slash ? 2 : doubleStar ? 1 : 0;
        }

        return pattern.Append('$').ToString();
    }

    private readonly record struct Spot(bool DoubleStar, bool Slash, bool Opens, bool Closes, bool Alternation);

    private static string Piece(char c, Spot spot) => c switch
    {
        '*' when spot.DoubleStar => spot.Slash ? "(?:.*/)?" : ".*",
        '*' => "[^/]*",
        '?' => "[^/]",
        _ when spot.Opens => "(?:",
        _ when spot.Closes => ")",
        ',' when spot.Alternation => "|",
        _ => Regex.Escape(c.ToString()),
    };

    // The positions of matched braces, and of the commas inside a matched pair.
    private static (HashSet<int> Open, HashSet<int> Close, HashSet<int> Inside) Braces(string glob)
    {
        HashSet<int> open = [];
        HashSet<int> close = [];
        HashSet<int> inside = [];
        Stack<List<int>> stack = new();
        for (var i = 0; i < glob.Length; i++)
        {
            switch (glob[i])
            {
                case '{':
                    stack.Push([i]);
                    break;
                case ',' when stack.Count > 0:
                    stack.Peek().Add(i);
                    break;
                case '}' when stack.Count > 0:
                    var group = stack.Pop();
                    _ = open.Add(group[0]);
                    _ = close.Add(i);
                    inside.UnionWith(group.Skip(1));
                    break;
                default:
                    break;
            }
        }

        return (open, close, inside);
    }
}
