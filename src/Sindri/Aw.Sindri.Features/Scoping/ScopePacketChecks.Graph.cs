namespace Aw.Sindri.Features.Scoping;

internal static partial class ScopePacketChecks
{
    /// <summary>
    /// SCP-GRAPH-001 (issue 1005): every graph node id a surface cites is in fact:graph. A surface that says where it came from (origin or nodes) must back it up:
    /// an origin of 'graph' cites at least one node, and a mapped surface's own key is among its nodes. A surface found only by search (origin 'search') may cite none.
    /// </summary>
    private static void GraphCited(ScopeContext c, RuleSink s)
    {
        if (!s.Need("graph", "scopeMap"))
        {
            return;
        }

        var ids = ScopeJson.Items(c.Facts["graph"], "nodes").Select(static n => ScopeJson.Str(n, "id")).OfType<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var surface in ScopeJson.Items(c.Facts["scopeMap"], "surfaces"))
        {
            var id = ScopeJson.Str(surface, "id");
            var nodes = ScopeJson.Strings(surface, "nodes").ToList();
            var origin = ScopeJson.Str(surface, "origin");
            foreach (var node in nodes.Where(n => !ids.Contains(n)).Distinct(StringComparer.Ordinal))
            {
                s.Fail(id, $"surface {id} cites {node}, which is not a node of fact:graph", "Cite only ids from graph.json; a surface found by your own search has origin 'search' and no nodes.");
            }

            if (string.Equals(origin, "graph", StringComparison.Ordinal) && nodes.Count == 0)
            {
                s.Fail(id, $"surface {id} has origin 'graph' but cites no graph node", "List the graph node ids the surface came from in nodes, or set origin to 'search'.");
            }

            var key = ScopeJson.Str(surface, "key");
            if (origin is not null && key is not null && string.Equals(ScopeJson.Str(surface, "graphStatus"), "mapped", StringComparison.Ordinal) && !nodes.Contains(key, StringComparer.Ordinal))
            {
                s.Fail(id, $"surface {id} is mapped to {key} but does not list it among its nodes", "Add the key to the surface's nodes, or mark the surface as not mapped.");
            }
        }
    }
}
