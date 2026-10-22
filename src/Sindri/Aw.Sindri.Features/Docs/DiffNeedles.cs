using System.Text.RegularExpressions;

namespace Aw.Sindri.Features.Docs;

/// <summary>The names a pull request's added lines introduce that a doc may describe (docs-sync spec §7.2): declared types and methods, long command flags, environment variables and dotted config keys. Short names are left out so common words don't match every doc.</summary>
internal static partial class DiffNeedles
{
    private const int MinName = 6;   // a shorter type or method name ('Run', 'Cache') would match half the docs

    private const int MinFlag = 8;   // '--help' and '--force' are in every CLI doc; '--dry-run' is not

    private const int MinEnvVar = 8;   // 'MAX_N' and 'A_B' are too common; 'AW_STATE_DIR' is not

    private const int MinKey = 10;

    private const int MaxNeedles = 200;

    /// <summary>The needles the added lines of the files that are not docs hold.</summary>
    /// <param name="added">The added lines with their paths.</param>
    /// <param name="isDoc">Whether a path is a doc (its lines are not searched).</param>
    /// <returns>The distinct needles, at most 200.</returns>
    internal static List<string> From(IEnumerable<(string Path, int Line, string Text)> added, Func<string, bool> isDoc) => [.. added
        .Where(line => !isDoc(line.Path))
        .SelectMany(static line => Of(line.Text))
        .Distinct(StringComparer.Ordinal)
        .Take(MaxNeedles)];

    private static IEnumerable<string> Of(string line)
    {
        foreach (var pattern in new[] { TypeDeclaration, FunctionDeclaration, MemberDeclaration })
        {
            foreach (Match match in pattern.Matches(line))
            {
                if (match.Groups["name"].Length >= MinName)
                {
                    yield return match.Groups["name"].Value;
                }
            }
        }

        foreach (Match match in Flag.Matches(line))
        {
            if (match.Length >= MinFlag)
            {
                yield return match.Value;
            }
        }

        foreach (Match match in EnvVar.Matches(line))
        {
            if (match.Length >= MinEnvVar)
            {
                yield return match.Value;
            }
        }

        foreach (Match match in ConfigKey.Matches(line))
        {
            if (match.Groups["key"].Length >= MinKey)
            {
                yield return match.Groups["key"].Value;
            }
        }
    }

    [GeneratedRegex(@"\b(?:class|interface|struct|record|enum|trait)\s+(?<name>[A-Za-z_]\w*)", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex TypeDeclaration { get; }

    [GeneratedRegex(@"\b(?:function|def|fn|func)\s+(?<name>[A-Za-z_]\w*)\s*[(<]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex FunctionDeclaration { get; }

    [GeneratedRegex(@"\b(?:public|protected|internal|export)\b[^(=;{]*?\b(?<name>[A-Za-z_]\w*)\s*[(<]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex MemberDeclaration { get; }

    [GeneratedRegex(@"(?<![\w-])--[a-z][a-z0-9]*(?:-[a-z0-9]+)*", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex Flag { get; }

    [GeneratedRegex(@"\b[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+\b", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex EnvVar { get; }

    [GeneratedRegex(@"[""'`](?<key>[a-z][a-z0-9]*(?:\.[a-z][A-Za-z0-9-]*){2,})[""'`]", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 200)]
    private static partial Regex ConfigKey { get; }
}
