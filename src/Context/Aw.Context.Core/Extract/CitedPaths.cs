using System.Text.RegularExpressions;

namespace Aw.Context.Core.Extract;

/// <summary>
/// The repo paths a doc's text cites: a backticked path (with an optional ':12' or ':12-20' line suffix) and a bare
/// 'file.ext:12' reference. A candidate is a path of letters, digits and '._-/' whose last segment has an extension; one
/// without a slash must carry a known source or doc extension, so a backticked 'string.Empty' is not a citation.
/// </summary>
internal static partial class CitedPaths
{
    private static readonly HashSet<string> _bareExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        "cs", "csproj", "slnx", "ts", "tsx", "js", "jsx", "mjs", "json", "yaml", "yml", "toml", "md", "sql", "sh", "py", "go", "rs", "java", "kt", "swift", "xml", "props", "txt",
    };

    /// <summary>Finds the cited paths.</summary>
    /// <param name="text">The doc's text.</param>
    /// <returns>The distinct cited paths, in order of first mention.</returns>
    internal static IReadOnlyList<string> Find(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var found = Backticked.Matches(text).Select(m => (m.Index, Text: m.Groups["t"].Value))
            .Concat(WithLine.Matches(text).Select(m => (m.Index, Text: m.Groups["t"].Value)))
            .OrderBy(h => h.Index)
            .Select(h => Normalize(h.Text))
            .OfType<string>();
        return [.. found.Distinct(StringComparer.Ordinal)];
    }

    private static string? Normalize(string candidate)
    {
        var path = Suffix.Replace(candidate, string.Empty);
        while (path.StartsWith("./", StringComparison.Ordinal))
        {
            path = path[2..];
        }

        return IsPath(path) ? path : null;
    }

    private static bool IsPath(string path)
    {
        if (path.Length == 0 || path[0] == '/' || path.EndsWith('/') || path.Contains("..", StringComparison.Ordinal) || path.Contains("//", StringComparison.Ordinal) || !PathShape.IsMatch(path))
        {
            return false;
        }

        var name = path[(path.LastIndexOf('/') + 1)..];
        var dot = name.LastIndexOf('.');
        return dot > 0 && dot < name.Length - 1 && (path.Contains('/', StringComparison.Ordinal) || _bareExtensions.Contains(name[(dot + 1)..]));
    }

    [GeneratedRegex("`(?<t>[^`\\s]{1,300})`", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Backticked { get; }

    [GeneratedRegex("(?<![\\w/.:-])(?<t>(?:[\\w.-]+/)*[\\w-]+\\.[A-Za-z0-9]{1,8}:[0-9]+(?:-[0-9]+)?)(?![\\w/])", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WithLine { get; }

    [GeneratedRegex(":[0-9]+(?:-[0-9]+)?\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Suffix { get; }

    [GeneratedRegex("^[A-Za-z0-9._/-]+\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PathShape { get; }
}
