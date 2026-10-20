using System.Text.Json.Nodes;
using Aw.Sindri.Core.Line;

namespace Aw.Sindri.Features.Design;

/// <summary>The ids a design packet's citations can resolve to, and the small readers the rules share.</summary>
internal static partial class DesignPacketChecks
{
    private const string ScopeMap = "scope/scope-map.json";
    private const string ScopeAcceptance = "scope/acceptance.json";

    /// <summary>The scope packet's surfaces as (id, key).</summary>
    private static IEnumerable<(string Id, string Key)> ScopeSurfaces(LineFacts facts)
    {
        foreach (var surface in LineJson.Items(facts.Json(ScopeMap), "surfaces"))
        {
            if (LineJson.Str(surface, "id") is { } id && LineJson.Str(surface, "key") is { } key)
            {
                yield return (id, key);
            }
        }
    }

    private static IEnumerable<JsonNode> Sections(LineFacts facts) => LineJson.Items(facts.Json("sections.json"), "sections");

    private static IEnumerable<JsonNode> Decisions(LineFacts facts) => LineJson.Items(facts.Json("decisions.json"), "decisions");

    private static IEnumerable<JsonNode> Items(LineFacts facts) => LineJson.Items(facts.Json("acceptance.json"), "items");

    /// <summary>The nodes by id; when an id repeats, the first wins (a repeated id is a shape fault the packet's own validation reports).</summary>
    private static Dictionary<string, JsonNode> ById(IEnumerable<JsonNode> nodes) =>
        nodes.GroupBy(static n => LineJson.Str(n, "id") ?? string.Empty, StringComparer.Ordinal).ToDictionary(static g => g.Key, static g => g.First(), StringComparer.Ordinal);

    private static bool Live(JsonNode item) => !string.Equals(LineJson.Str(item, "status"), "superseded", StringComparison.Ordinal);

    private static HashSet<string> Ids(IEnumerable<JsonNode> nodes) => new(nodes.Select(static n => LineJson.Str(n, "id")).OfType<string>(), StringComparer.Ordinal);

    /// <summary>Whether a citation resolves: a scope surface, an acceptance item (the design's or the scope's), a decision or section of this design, or an answer, ref or lens item (by shape when the scope file is absent).</summary>
    private sealed class Citations
    {
        private readonly HashSet<string> _known = new(StringComparer.Ordinal);
        private readonly LineFacts _facts;

        internal Citations(LineFacts facts)
        {
            _facts = facts;
            _known.UnionWith(ScopeSurfaces(facts).Select(static s => s.Id));
            _known.UnionWith(Ids(Items(facts)));
            _known.UnionWith(Ids(LineJson.Items(facts.Json(ScopeAcceptance), "items")));
            _known.UnionWith(Ids(Decisions(facts)));
            _known.UnionWith(Ids(Sections(facts)));
            _known.UnionWith(Ids(LineJson.Items(facts.Json("scope/answers.jsonl"))));
            _known.UnionWith(LensItems(facts.Json("scope/lenses.json")));
        }

        internal bool Resolves(string citation) =>
            _known.Contains(citation)
            || (!_facts.Has("scope/answers.jsonl") && Shaped(citation, "A"))
            || (!_facts.Has("scope/sources.json") && Shaped(citation, "R"))
            || (!_facts.Has("scope/lenses.json") && citation.StartsWith("L-", StringComparison.Ordinal))
            || (!_facts.Has(ScopeMap) && Shaped(citation, "S"));

        private static bool Shaped(string citation, string prefix) =>
            citation.Length > prefix.Length && citation.StartsWith(prefix, StringComparison.Ordinal) && citation[prefix.Length..].All(char.IsAsciiDigit);

        private static IEnumerable<string> LensItems(JsonNode? lenses)
        {
            foreach (var lens in new[] { "code", "product", "related", "history" })
            {
                var entry = (lenses as JsonObject)?[lens];
                foreach (var node in LineJson.Items(entry, "nodes").Concat(LineJson.Items(entry, "items")))
                {
                    if (LineJson.Str(node, "lensItem") is { } item)
                    {
                        yield return item;
                    }
                }
            }
        }
    }
}
