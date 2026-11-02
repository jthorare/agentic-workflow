using System.Text.RegularExpressions;

namespace Aw.Context.Core.Extract.Imports;

/// <summary>The C# usings that name a namespace some other file of the repo declares.</summary>
internal static partial class CsImports
{
    internal static ImportScan Find(IReadOnlyList<SourceFile> files, int maxDeclaringFiles)
    {
        var sources = files.Where(f => f.Path.EndsWith(".cs", StringComparison.Ordinal)).Select(f => (f.Path, Text: Comments.Strip(f.Text))).ToList();
        try
        {
            var declared = Declarations(sources);
            var pairs = new List<ImportPair>();
            var skipped = new HashSet<string>(StringComparer.Ordinal);
            foreach (var (path, text) in sources)
            {
                foreach (var ns in Used(text).Select(u => Declared(declared, u.Name, u.IsStatic)).OfType<string>().Distinct(StringComparer.Ordinal))
                {
                    if (declared[ns].Count > maxDeclaringFiles)
                    {
                        skipped.Add(ns);
                    }
                    else
                    {
                        pairs.AddRange(declared[ns].Select(to => new ImportPair(path, to)));
                    }
                }
            }

            return new ImportScan(pairs, skipped.Count);
        }
        catch (RegexMatchTimeoutException)
        {
            return new ImportScan([], 0);   // why: a file that defeats the pattern gives no edges rather than failing the map
        }
    }

    private static Dictionary<string, List<string>> Declarations(List<(string Path, string Text)> sources) =>
        sources
            .SelectMany(s => Namespace.Matches(s.Text).Select(m => (Name: m.Groups["n"].Value, s.Path)))
            .GroupBy(d => d.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(d => d.Path).Distinct(StringComparer.Ordinal).ToList(), StringComparer.Ordinal);

    private static IEnumerable<(string Name, bool IsStatic)> Used(string text) =>
        Using.Matches(text).Select(m => (m.Groups["n"].Value, m.Groups["st"].Success));

    /// <summary>The declared namespace a using names; a 'using static' names a type, so trailing segments drop until a namespace matches.</summary>
    private static string? Declared(Dictionary<string, List<string>> declared, string name, bool isStatic)
    {
        if (!isStatic)
        {
            return declared.ContainsKey(name) ? name : null;
        }

        for (var dot = name.LastIndexOf('.'); dot > 0; dot = name.LastIndexOf('.', dot - 1))
        {
            if (declared.ContainsKey(name[..dot]))
            {
                return name[..dot];
            }
        }

        return null;
    }

    [GeneratedRegex("^[ \\t]*namespace\\s+(?<n>[A-Za-z_][\\w.]*)\\s*[;{]", RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Namespace { get; }

    [GeneratedRegex("^[ \\t]*(?:global\\s+)?using\\s+(?<st>static\\s+)?(?<n>[A-Za-z_][\\w.]*)\\s*;", RegexOptions.CultureInvariant | RegexOptions.Multiline | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Using { get; }
}
