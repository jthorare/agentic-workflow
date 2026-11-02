using System.Text.RegularExpressions;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;

namespace Aw.Context.Core.Extract;

/// <summary>
/// Derives the actions a repo's surfaces offer (S-4): a named click or submit handler in a component, a state-changing route and a titled
/// command. Each is a candidate (confidence 0.8, a later step decides), 'on' the surface the recognizers found, 'implements'-linked from its
/// file and 'writes' what its file writes. Pure and ordered.
/// </summary>
internal static partial class ActionPass
{
    private const double Confidence = 0.8;
    private static readonly Dictionary<string, string> _verbs = new(StringComparer.Ordinal) { ["POST"] = "Create", ["PUT"] = "Update", ["PATCH"] = "Update", ["DELETE"] = "Delete" };

    /// <summary>Derives the actions from the files and the map found so far.</summary>
    /// <param name="repo">The repo (its name joins the evidence keys).</param>
    /// <param name="files">The files at the commit.</param>
    /// <param name="found">The surfaces and the 'writes' edges already found.</param>
    /// <returns>The actions, their code files and their edges.</returns>
    internal static ExtractedMap Run(ContextRepoName repo, IReadOnlyList<RepoFile> files, ExtractedMap found)
    {
        var surfaces = found.Nodes.Select(n => n.Key.Value).ToHashSet(StringComparer.Ordinal);
        var writes = found.Edges.Where(e => e.Type == EdgeType.Writes).ToLookup(e => e.From.Value, StringComparer.Ordinal);
        List<GraphNode> nodes = [];
        List<GraphEdge> edges = [];
        foreach (var file in files.Where(Recognizers.Source).OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            if (MapExtractor.CodeKey(file.Path) is not { } code || MapExtractor.Evidence(repo, file.Path) is not { } evidence)
            {
                continue;
            }

            foreach (var (key, label, surface) in Derive(file))
            {
                if (!NodeKey.TryFrom(key, out var action) || action.Kind != NodeKind.Action || !NodeKey.TryFrom(surface, out var on) || !surfaces.Contains(on.Value))
                {
                    continue;
                }

                nodes.Add(new GraphNode(action, label, Provenance.Deterministic, Confidence, [evidence]));
                edges.Add(new GraphEdge(code, action, EdgeType.Implements, Provenance.Deterministic, 1.0, [evidence]));
                edges.Add(new GraphEdge(action, on, EdgeType.On, Provenance.Deterministic, 1.0, [evidence]));
                edges.AddRange(writes[code.Value].Select(w => new GraphEdge(action, w.To, EdgeType.Writes, Provenance.Deterministic, 1.0, [evidence])));
                nodes.Add(MapExtractor.CodeNode(file.Path));
            }
        }

        return ExtractedMap.Empty.Plus(new ExtractedMap(nodes, edges));
    }

    private static IEnumerable<(string Key, string Label, string Surface)> Derive(RepoFile file)
    {
        if (Recognizers.IsComponentFile(file.Path))
        {
            foreach (var action in Handlers(file.Text))
            {
                yield return action;
            }
        }

        if (Recognizers.Script(file))
        {
            foreach (var action in Routes(Recognizers.Route.Matches(file.Text)))
            {
                yield return action;
            }

            foreach (Match m in Recognizers.RegistryEntry.Matches(file.Text))
            {
                var id = m.Groups["id"].Value;
                if (m.Groups["title"].Value.Length > 0)
                {
                    yield return ($"action:command/{id}", m.Groups["title"].Value, $"surface:command/{id}");
                }
            }
        }

        if (Recognizers.IsCSharp(file.Path))
        {
            foreach (var action in Routes(Recognizers.MinimalApiRoute.Matches(file.Text)))
            {
                yield return action;
            }
        }
    }

    private static IEnumerable<(string Key, string Label, string Surface)> Handlers(string text)
    {
        var components = Recognizers.ComponentNames(text).ToList();
        foreach (Match m in Handler.Matches(text))
        {
            if (components.LastOrDefault(c => c.Index < m.Index) is not { Name: { } component })
            {
                continue;
            }

            var words = Words(m.Groups["h"].Value);
            if (words.Count > 0)
            {
                var label = string.Join(' ', words);
                yield return ($"action:{component}.{string.Join('-', words)}", char.ToUpperInvariant(label[0]) + label[1..], $"surface:{Recognizers.ComponentKind(component)}/{component}");
            }
        }
    }

    private static List<string> Words(string handler)
    {
        var name = handler;
        foreach (var prefix in new[] { "handle", "on" })
        {
            if (name.Length > prefix.Length && name.StartsWith(prefix, StringComparison.Ordinal) && char.IsUpper(name[prefix.Length]))
            {
                name = name[prefix.Length..];
                break;
            }
        }

        return [.. CamelWord.Matches(name).Select(w => w.Value.ToLowerInvariant())];
    }

    private static IEnumerable<(string Key, string Label, string Surface)> Routes(MatchCollection matches)
    {
        foreach (Match m in matches)
        {
            var method = m.Groups["m"].Value.ToUpperInvariant();
            if (_verbs.TryGetValue(method, out var verb))
            {
                var path = m.Groups["p"].Value;
                var noun = path.Split('/').LastOrDefault(s => s.Length > 0 && s[0] is not (':' or '{' or '*'))?.TrimEnd('s');
                yield return ($"action:route/{method}:{path}", string.IsNullOrEmpty(noun) ? verb : $"{verb} {noun}", $"surface:route/{method}:{path}");
            }
        }
    }

    [GeneratedRegex("\\bon(?:Click|Submit)=\\{\\s*(?<h>[A-Za-z_]\\w*)\\s*\\}", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Handler { get; }

    [GeneratedRegex("[A-Z]?[a-z0-9]+|[A-Z]+(?![a-z])", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CamelWord { get; }
}
