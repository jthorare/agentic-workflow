using System.Text.RegularExpressions;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Extract;

/// <summary>
/// Domain concepts from a repo's glossary. A file named CONTEXT.md or GLOSSARY.md (any directory, case ignored) lists terms as
/// <c>- **Term** (aliases: a, b) - definition</c> (a bullet or a bare line; 'aliases', 'also' or 'aka' name the aliases). In GLOSSARY.md every
/// such line is a term; in CONTEXT.md only the lines under a heading that contains 'glossary', up to the next heading of the same or a higher level.
/// Each term becomes a deterministic feature node 'feature:glossary/&lt;slug&gt;' implemented by its file, and each alias an alias key of the same shape.
/// </summary>
internal static partial class Glossary
{
    private const int MaxTermsPerFile = 500;
    private const int MaxSlug = 120;

    /// <summary>Whether a path is a glossary file.</summary>
    /// <param name="path">The path from the repo root.</param>
    /// <returns>True for CONTEXT.md and GLOSSARY.md.</returns>
    internal static bool IsGlossaryFile(string path)
    {
        var name = path[(path.LastIndexOf('/') + 1)..];
        return name.Equals("CONTEXT.md", StringComparison.OrdinalIgnoreCase) || name.Equals("GLOSSARY.md", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The key a name would have as a glossary term: its letter and digit runs, lower case, joined by '-'.</summary>
    /// <param name="name">A term, an alias or what a caller typed.</param>
    /// <returns>The key, or null when the name has no letter or digit.</returns>
    internal static NodeKey? KeyFor(string name)
    {
        var slug = string.Join('-', Runs().Matches(name).Select(m => m.Value.ToLowerInvariant()));
        if (slug.Length == 0)
        {
            return null;
        }

        return NodeKey.TryFrom("feature:glossary/" + (slug.Length > MaxSlug ? slug[..MaxSlug] : slug), out var key) ? key : null;
    }

    /// <summary>The concepts the repo's glossary files define.</summary>
    /// <param name="repo">The repo (its name joins the evidence keys).</param>
    /// <param name="files">The files at the commit.</param>
    /// <returns>The nodes, their file links and the aliases, ordered.</returns>
    internal static ExtractedMap Extract(ContextRepoName repo, IReadOnlyList<RepoFile> files)
    {
        var nodes = new List<GraphNode>();
        var edges = new List<GraphEdge>();
        var aliases = new List<NodeAlias>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var file in files.Where(f => IsGlossaryFile(f.Path)).OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            if (!NodeKey.TryFrom("code:" + file.Path, out var code) || !RecordKey.TryFrom($"repo:{repo.Value}/{file.Path}", out var record))
            {
                continue;
            }

            var evidence = new[] { new EvidenceRef(record) };
            var any = false;
            foreach (var (term, names) in Terms(file))
            {
                if (KeyFor(term) is not { } key || !seen.Add(key.Value))
                {
                    continue;
                }

                any = true;
                nodes.Add(new GraphNode(key, term, Provenance.Deterministic, 1.0, evidence));
                edges.Add(new GraphEdge(code, key, EdgeType.Implements, Provenance.Deterministic, 1.0, evidence));
                foreach (var alias in names.Select(KeyFor).OfType<NodeKey>().Where(a => seen.Add(a.Value)))
                {
                    aliases.Add(new NodeAlias(alias, key));
                }
            }

            if (any)
            {
                nodes.Add(new GraphNode(code, file.Path[(file.Path.LastIndexOf('/') + 1)..], Provenance.Deterministic));
            }
        }

        return new ExtractedMap(nodes, edges) { Aliases = aliases };
    }

    private static IEnumerable<(string Term, List<string> Aliases)> Terms(RepoFile file)
    {
        var whole = file.Path[(file.Path.LastIndexOf('/') + 1)..].Equals("GLOSSARY.md", StringComparison.OrdinalIgnoreCase);
        var level = whole ? 0 : -1;
        var count = 0;
        foreach (var line in file.Text.Split('\n'))
        {
            if (Heading().Match(line) is { Success: true } heading)
            {
                var depth = heading.Groups["hashes"].Length;
                if (!whole && heading.Groups["title"].Value.Contains("glossary", StringComparison.OrdinalIgnoreCase))
                {
                    level = depth;
                }
                else if (!whole && level >= 0 && depth <= level)
                {
                    level = -1;
                }

                continue;
            }

            if (level >= 0 && Entry().Match(line) is { Success: true } entry && count < MaxTermsPerFile)
            {
                count++;
                var term = entry.Groups["term"].Value.Trim();
                yield return (term, Aliases(entry.Groups["rest"].Value));
            }
        }
    }

    private static List<string> Aliases(string rest) =>
        AliasList().Match(rest) is { Success: true } m ? [.. m.Groups["names"].Value.Split([',', ';']).Select(a => a.Trim()).Where(a => a.Length > 0)] : [];

    [GeneratedRegex("[\\p{L}\\p{N}]+", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Runs();

    [GeneratedRegex("^(?<hashes>#{1,6})\\s+(?<title>.*)$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Heading();

    [GeneratedRegex("^\\s*(?:[-*+]\\s+)?\\*\\*(?<term>[^*\\r\\n]{1,120})\\*\\*(?<rest>.*)$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Entry();

    [GeneratedRegex("^\\s*\\((?:aliases?|also|aka|a\\.k\\.a\\.)\\s*[:\\s]\\s*(?<names>[^)]*)\\)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex AliasList();
}
