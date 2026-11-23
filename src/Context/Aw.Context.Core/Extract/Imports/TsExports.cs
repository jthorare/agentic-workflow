using System.Text.RegularExpressions;

namespace Aw.Context.Core.Extract.Imports;

/// <summary>Reads what a TS or JS file exports (comments are ignored).</summary>
internal static partial class TsExports
{
    internal static ModuleExports Read(string text)
    {
        try
        {
            var clean = Comments.Strip(text);
            var declared = new HashSet<string>(Declaration.Matches(clean).Select(m => m.Groups["n"].Value), StringComparer.Ordinal);
            var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
            var reExports = new Dictionary<string, (string Specifier, string Name)>(StringComparer.Ordinal);
            foreach (Match m in List.Matches(clean))
            {
                if (m.Groups["t"].Success)
                {
                    continue;   // export type { ... }
                }

                foreach (var (exported, name) in Names(m.Groups["names"].Value))
                {
                    if (m.Groups["s"].Success)
                    {
                        reExports[exported] = (m.Groups["s"].Value, name);
                    }
                    else
                    {
                        aliases[exported] = name;
                    }
                }
            }

            var (namespaces, stars) = StarsOf(clean);
            return new ModuleExports(declared, aliases, reExports, namespaces, stars);
        }
        catch (RegexMatchTimeoutException)
        {
            return ModuleExports.Empty;
        }
    }

    // export * from 'x' (a star) and export * as Ns from 'x' (a namespace; a later one of the same name wins, as TS reports the clash anyway); the type-only
    // forms 'export type * from' and 'export type * as Ns from' re-export the same names, so they count the same (issue 4043)
    private static (Dictionary<string, string> Namespaces, List<string> Stars) StarsOf(string clean)
    {
        var namespaces = new Dictionary<string, string>(StringComparer.Ordinal);
        var stars = new List<string>();
        foreach (Match m in Star.Matches(clean))
        {
            if (m.Groups["ns"].Success)
            {
                namespaces[m.Groups["ns"].Value] = m.Groups["s"].Value;
            }
            else
            {
                stars.Add(m.Groups["s"].Value);
            }
        }

        return (namespaces, stars);
    }

    // a, b as c, default as d  ->  (exported, name)
    private static IEnumerable<(string Exported, string Name)> Names(string list)
    {
        foreach (var item in list.Split(','))
        {
            var words = item.Split([' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 3 && string.Equals(words[1], "as", StringComparison.Ordinal))
            {
                yield return (words[2], words[0]);
            }
            else if (words.Length == 1)
            {
                yield return (words[0], words[0]);
            }
        }
    }

    [GeneratedRegex("\\bexport\\s+(?:declare\\s+)?(?:async\\s+)?(?:const|let|var|function\\*?|class|enum|abstract\\s+class)\\s+(?<n>[A-Za-z_$][\\w$]*)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Declaration { get; }

    [GeneratedRegex("\\bexport\\s*(?<t>type\\s+)?\\{(?<names>[^}]*)\\}(?:\\s*from\\s*(?<q>[\"'])(?<s>[^\"'\\r\\n]+)\\k<q>)?", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex List { get; }

    [GeneratedRegex("\\bexport\\s*(?:type\\s+)?\\*\\s*(?:as\\s+(?<ns>[\\w$]+)\\s*)?from\\s*(?<q>[\"'])(?<s>[^\"'\\r\\n]+)\\k<q>", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Star { get; }
}
