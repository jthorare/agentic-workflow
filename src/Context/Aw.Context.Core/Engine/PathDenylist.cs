using System.Collections.Concurrent;
using System.Text;
using System.Text.RegularExpressions;

namespace Aw.Context.Core.Engine;

/// <summary>
/// The paths a repo's extraction never reads (S-4, the profile's 'index.denyPaths' and the built-in secret globs): they never
/// reach a recognizer, the clustering snapshot or the labeler. Globs are repo-relative with '/' separators, matched without
/// regard to case: '**/' is any directories, a trailing '**' is anything, '*' stays inside one segment, '?' is one character. A glob that matches a directory
/// matches everything under it, as in .gitignore ('**/*cron*' denies 'src/crons/run.ts' and 'src/nightly-cron.ts').
/// A role entry may instead be tagged: 'prefix:test_' and 'suffix:.generated.ts' match when any path segment (a directory or the
/// file name) starts or ends with the text, and 'regex:^old_.*' when any segment matches the regular expression.
/// </summary>
public static class PathDenylist
{
    /// <summary>The secret globs every repo has.</summary>
    public static IReadOnlyList<string> Defaults { get; } =
    [
        ".env*", "**/.env*", "**/*.pem", "**/*.key", "**/*.p12", "**/*.pfx", "**/id_rsa*", "**/*.tfstate", "**/*.tfvars",
        "**/credentials*", "**/.npmrc", "**/.netrc", "**/secrets/**",
    ];

    /// <summary>Whether a path matches any glob.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <param name="globs">The globs.</param>
    /// <returns>True when the path is denied.</returns>
    public static bool Matches(string path, IReadOnlyList<string> globs)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(globs);
        return globs.Any(g => Tagged(g, path, ignoreCase: true) ?? _compiled.GetOrAdd(g, Compile).IsMatch(path));
    }

    /// <summary>Whether a path matches any glob, with the case of the path and the glob compared exactly.</summary>
    /// <param name="path">The repo-relative path.</param>
    /// <param name="globs">The globs.</param>
    /// <returns>True when a glob matches.</returns>
    public static bool MatchesExactCase(string path, IReadOnlyList<string> globs)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(globs);
        return globs.Any(g => Tagged(g, path, ignoreCase: false) ?? _compiledExact.GetOrAdd(g, glob => Compile(glob, RegexOptions.None)).IsMatch(path));
    }

    private static readonly string[] _tags = ["prefix:", "suffix:", "regex:"];

    // A tagged entry: null when the entry is a plain glob.
    private static bool? Tagged(string entry, string path, bool ignoreCase)
    {
        var tag = _tags.FirstOrDefault(t => entry.StartsWith(t, StringComparison.Ordinal));
        if (tag is null)
        {
            return null;
        }

        var text = entry[tag.Length..];
        var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var segments = path.Split('/');
        return tag switch
        {
            "prefix:" => text.Length > 0 && segments.Any(s => s.StartsWith(text, comparison)),
            "suffix:" => text.Length > 0 && segments.Any(s => s.EndsWith(text, comparison)),
            _ => segments.Any(s => SegmentRegex(text, ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None).IsMatch(s)),
        };
    }

    private static Regex SegmentRegex(string pattern, RegexOptions options) =>
        _segmentRegexes.GetOrAdd((pattern, options), k => new Regex(k.Pattern, RegexOptions.CultureInvariant | k.Options, TimeSpan.FromSeconds(1)));

    private static readonly ConcurrentDictionary<(string Pattern, RegexOptions Options), Regex> _segmentRegexes = new();

    // Each glob is compiled once per process: a build asks per tracked file.
    private static readonly ConcurrentDictionary<string, Regex> _compiled = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, Regex> _compiledExact = new(StringComparer.Ordinal);

    private static Regex Compile(string glob) => Compile(glob, RegexOptions.IgnoreCase);

    private static Regex Compile(string glob, RegexOptions caseOption)
    {
        var pattern = new StringBuilder("^");
        for (var i = 0; i < glob.Length; i++)
        {
            var c = glob[i];
            if (c == '*' && i + 1 < glob.Length && glob[i + 1] == '*')
            {
                if (i + 2 < glob.Length && glob[i + 2] == '/')
                {
                    _ = pattern.Append("(?:.*/)?");
                    i += 2;
                }
                else
                {
                    _ = pattern.Append(".*");
                    i++;
                }
            }
            else
            {
                _ = c switch
                {
                    '*' => pattern.Append("[^/]*"),
                    '?' => pattern.Append("[^/]"),
                    _ => pattern.Append(Regex.Escape(c.ToString())),
                };
            }
        }

        return new Regex(pattern.Append("(?:/.*)?$").ToString(), RegexOptions.CultureInvariant | caseOption | RegexOptions.Singleline, TimeSpan.FromSeconds(1));
    }
}
