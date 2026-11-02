using Aw.Context.Core.Extract;
using Aw.Context.Core.Graph;
using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Tests.Extract;

/// <summary>The seven-file, three-module, two-table fixture of spec 2026-10-27 §3, as the file-level edges steps 1 to 3 leave in the extraction.</summary>
internal static class RollupFixture
{
    internal const string Billing = "feature:work.billing";
    internal const string Orders = "feature:work.orders";
    internal const string Users = "feature:work.users";

    internal static ExtractedMap Files() => Files(homes: [("b1", "billing"), ("b2", "billing"), ("b3", "billing"), ("o1", "orders"), ("o2", "orders"), ("u1", "users"), ("u2", "users")],
        imports: [("b1", "o1"), ("b2", "o1"), ("b2", "o2"), ("b3", "b1"), ("o1", "u1")],
        data: [("b1", EdgeType.Writes, "invoices"), ("b2", EdgeType.Reads, "invoices"), ("b3", EdgeType.Writes, "orders_t"), ("o1", EdgeType.Reads, "invoices"), ("o2", EdgeType.Writes, "orders_t"), ("u1", EdgeType.Reads, "orders_t")]);

    /// <summary>A file's path: 'billing/b1.ts' for 'b1' homed in billing.</summary>
    internal static string Path(string file, IEnumerable<(string File, string Module)> homes) => (homes.FirstOrDefault(h => h.File == file).Module ?? "nohome") + "/" + file + ".ts";

    internal static ExtractedMap Files(IReadOnlyList<(string File, string Module)> homes, IReadOnlyList<(string From, string To)> imports, IReadOnlyList<(string File, EdgeType Type, string Thing)> data)
    {
        var nodes = new List<GraphNode>();
        var edges = new List<GraphEdge>();
        foreach (var module in homes.Select(h => h.Module).Distinct(StringComparer.Ordinal))
        {
            nodes.Add(new GraphNode(NodeKey.From("feature:work." + module), module, Provenance.Deterministic));
        }

        foreach (var (file, module) in homes)
        {
            nodes.Add(new GraphNode(NodeKey.From("code:" + Path(file, homes)), file + ".ts", Provenance.Deterministic));
            edges.Add(new GraphEdge(NodeKey.From("code:" + Path(file, homes)), NodeKey.From("feature:work." + module), EdgeType.Implements, Provenance.Deterministic, 1.0, Evidence: [new EvidenceRef(RecordKey.From("repo:work/" + Path(file, homes)))]));
        }

        foreach (var (from, to) in imports)
        {
            edges.Add(new GraphEdge(NodeKey.From("code:" + Path(from, homes)), NodeKey.From("code:" + Path(to, homes)), EdgeType.DependsOn, Provenance.Deterministic, 1.0));
        }

        foreach (var thing in data.Select(d => d.Thing).Distinct(StringComparer.Ordinal))
        {
            nodes.Add(new GraphNode(NodeKey.From("thing:" + thing), thing, Provenance.Deterministic));
        }

        foreach (var (file, type, thing) in data)
        {
            edges.Add(new GraphEdge(NodeKey.From("code:" + Path(file, homes)), NodeKey.From("thing:" + thing), type, Provenance.Deterministic, 1.0));
        }

        return new ExtractedMap(nodes, edges);
    }

    /// <summary>The roll-up edges of a result: (from, type, to, count, evidence paths).</summary>
    internal static List<(string From, EdgeType Type, string To, int? Count, string Evidence)> Rolled(ExtractedMap map) =>
        [.. map.Edges.Where(e => e.From.Kind == NodeKind.Feature && e.Type is EdgeType.DependsOn or EdgeType.Reads or EdgeType.Writes)
            .OrderBy(e => e.From.Value, StringComparer.Ordinal).ThenBy(e => e.To.Value, StringComparer.Ordinal).ThenBy(e => e.Type)
            .Select(e => (e.From.Value, e.Type, e.To.Value, e.Count, string.Join(' ', e.EvidenceOrEmpty.Select(r => r.Key.Value["repo:work/".Length..] + (r.Span is null ? string.Empty : " -> " + r.Span["repo:work/".Length..])))))];
}
