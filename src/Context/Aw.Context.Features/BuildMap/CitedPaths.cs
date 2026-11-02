using System.Text.RegularExpressions;

namespace Aw.Context.Features.BuildMap;

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

    /// <summary>Finds the backticked names that look like a symbol: an identifier of three or more characters with an upper-case letter or an underscore, so `true` and `value` are not looked up.</summary>
    /// <param name="text">The doc's text.</param>
    /// <returns>The distinct names, in order of first mention.</returns>
    internal static IReadOnlyList<string> FindSymbols(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return [.. Backticked.Matches(text).Select(m => Symbol(m.Groups["t"].Value)).OfType<string>().Distinct(StringComparer.Ordinal)];
    }

    // A backticked name that looks like code: an upper-case letter or an underscore (`TaxCalculator`, `MAX_ROWS`, `parse_args`), or a call or
    // member shape (`render()`, `config.load`, `a::b`), which names its last part. Plain words (`true`, `value`) and paths are not looked up.
    private static string? Symbol(string token)
    {
        var call = token.EndsWith("()", StringComparison.Ordinal);
        var name = call ? token[..^2] : token;
        var parts = name.Split(["::", "."], StringSplitOptions.None);
        var member = parts.Length > 1;
        if ((member && Normalize(name) is not null) || !parts.All(Identifier.IsMatch))
        {
            return null;
        }

        var last = parts[^1];
        return SymbolShape.IsMatch(last) || ((call || member) && last.Length >= 3) ? last : null;
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

    [GeneratedRegex("^(?=[A-Za-z0-9_]*[A-Z_])[A-Za-z_][A-Za-z0-9_]{2,}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SymbolShape { get; }

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Identifier { get; }

    [GeneratedRegex(":[0-9]+(?:-[0-9]+)?\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Suffix { get; }

    [GeneratedRegex("^[A-Za-z0-9._/-]+\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PathShape { get; }
}
