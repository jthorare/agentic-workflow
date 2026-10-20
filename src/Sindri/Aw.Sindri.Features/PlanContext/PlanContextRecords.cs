using System.Globalization;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Features.PlanContext;

/// <summary>What LN-CTX1 sends, derived from the design packet's files and the plan's answer log: the surfaces the map lacks, and the owner's picks as decision records.</summary>
internal static class PlanContextRecords
{
    private static readonly string[] _nodeKinds = ["feature", "surface", "action", "thing", "flow"];

    /// <summary>The surfaces the design adds beyond the map: the surface escapes and the paths the coverage could not map to a node, each once, in that order.</summary>
    internal static List<ProposedScopeSurface> Surfaces(LineFacts facts)
    {
        var surfaces = new List<ProposedScopeSurface>();
        foreach (var escape in LineJson.Items(facts.Json("design/escapes.json"), "escapes").Where(static e => LineJson.Str(e, "kind") == "surface"))
        {
            if (LineJson.Str(escape, "id") is { Length: > 0 } key)
            {
                var colon = key.IndexOf(':', StringComparison.Ordinal);
                var kind = colon > 0 && Array.IndexOf(_nodeKinds, key[..colon]) >= 0 ? key[..colon] : null;
                Add(surfaces, new ProposedScopeSurface(kind is null ? key : key[(colon + 1)..], kind));
            }
        }

        foreach (var path in LineJson.Items(facts.Json("design/coverage.json"), "unmapped").Select(static u => LineJson.Str(u, "path")).OfType<string>())
        {
            Add(surfaces, new ProposedScopeSurface(path, Kind: null));
        }

        return surfaces;
    }

    /// <summary>The owner's final pick of each decision as a decision record, in the design's order: its question and the option's answer, linked to the map nodes of the surfaces it cites, tied to the log entry the pick came from.</summary>
    internal static List<OwnerDecision> Decisions(LineFacts facts)
    {
        var picks = Picks(facts);
        var nodes = Nodes(facts);
        var records = new List<OwnerDecision>();
        foreach (var decision in LineJson.Items(facts.Json("design/decisions.json"), "decisions"))
        {
            var id = LineJson.Str(decision, "id") ?? string.Empty;
            var option = picks.TryGetValue(id, out var pick) ? LineJson.Items(decision, "options").FirstOrDefault(o => LineJson.Str(o, "id") == pick.Option) : null;
            if (option is not null)
            {
                var linked = LineJson.Strings(decision, "cites").Select(c => nodes.GetValueOrDefault(c)).OfType<string>().Distinct(StringComparer.Ordinal).ToList();
                records.Add(new OwnerDecision($"{LineJson.Str(decision, "question")} {LineJson.Str(option, "answer")}", linked, pick.Entry));
            }
        }

        return records;
    }

    private static void Add(List<ProposedScopeSurface> surfaces, ProposedScopeSurface surface)
    {
        if (!surfaces.Exists(s => string.Equals(s.Title, surface.Title, StringComparison.Ordinal)))
        {
            surfaces.Add(surface);
        }
    }

    // The latest pick of each decision, with the answer log entry it came from ('id', else 'A' and the batch number).
    private static Dictionary<string, (string Option, string Entry)> Picks(LineFacts facts)
    {
        var picks = new Dictionary<string, (string Option, string Entry)>(StringComparer.Ordinal);
        foreach (var entry in LineJson.Items(facts.Json("answers.jsonl")))
        {
            var name = LineJson.Str(entry, "id") ?? ("A" + (LineJson.Number(entry, "batch") ?? 0).ToString(CultureInfo.InvariantCulture));
            foreach (var answer in LineJson.Items(entry, "answers"))
            {
                if (LineJson.Str(answer, "decision") is { } decision && LineJson.Str(answer, "pick") is { } option)
                {
                    picks[decision] = (option, name);
                }
            }
        }

        return picks;
    }

    // A scope surface's id (S3) to its map node key, for the surfaces that have one (a 'code:' file key is not a node).
    private static Dictionary<string, string> Nodes(LineFacts facts)
    {
        var nodes = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var surface in LineJson.Items(facts.Json("scope/scope-map.json"), "surfaces").Concat(LineJson.Items(facts.Json("design/coverage.json"), "surfaces")))
        {
            if (LineJson.Str(surface, "id") is { } id && LineJson.Str(surface, "key") is { } key && key.IndexOf(':', StringComparison.Ordinal) is > 0 and var colon && Array.IndexOf(_nodeKinds, key[..colon]) >= 0)
            {
                nodes[id] = key;
            }
        }

        return nodes;
    }
}
