using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph.Interpolation;

namespace Aw.Sindri.Core.Graph.Validation;

public sealed partial class GraphValidator
{
    private static readonly HashSet<string> _scopeRoots = new(["trigger", "item", "workflow"], StringComparer.Ordinal);

    private void References(NodeScope scope, List<GraphFinding> found)
    {
        var seen = new HashSet<(string, string)>();
        foreach (var (field, text) in Strings(scope.Node.Config, "config"))
        {
            var refs = Template.References(text).ToList();
            if (string.Equals(scope.Node.Type, "condition", StringComparison.Ordinal) && string.Equals(field, "config.when", StringComparison.Ordinal))
            {
                refs.AddRange(Condition(scope.Node, field, text, found));
            }

            foreach (var reference in refs.Where(r => seen.Add((field, r))))
            {
                Reference(scope, field, reference, found);
            }
        }
    }

    private IReadOnlyList<string> Condition(GraphNode node, string field, string text, List<GraphFinding> found)
    {
        var parsed = context.Condition(text);
        if (parsed.Error is { } error)
        {
            found.Add(Found.At("GRAPH-COND-001", node.Id.Value, field, $"the condition can't be read: {error}", "write it with references, comparisons, and/or/not and exists(x)"));
        }

        return parsed.References;
    }

    private void Reference(NodeScope scope, string field, string reference, List<GraphFinding> found)
    {
        var id = scope.Node.Id.Value;
        var root = reference.Split('.', 2)[0];
        if (_scopeRoots.Contains(root))
        {
            return;
        }

        if (!scope.Structure.Has(root))
        {
            found.Add(Found.At("GRAPH-REF-003", id, field, $"'{reference}' names node '{root}', which does not exist", "use the id of an upstream node"));
        }
        else if (!scope.Upstream.Contains(root))
        {
            found.Add(Found.At("GRAPH-REF-001", id, field, $"'{reference}' reads node '{root}', which is not upstream of '{id}'", $"connect '{root}' before '{id}', or read from a node that runs earlier"));
        }
        else if (OutputProblem(scope.Structure.Find(root), reference) is { } problem)
        {
            found.Add(Found.At("GRAPH-REF-002", id, field, problem.Message, problem.Fix));
        }
    }

    private (string Message, string Fix)? OutputProblem(GraphNode? source, string reference)
    {
        if (source is null || !context.Blocks.TryGetValue(source.Type, out var block))
        {
            return null;
        }

        var parts = reference.Split('.');
        var name = parts.Length > 1 ? parts[1] : string.Empty;
        var fields = block.OutputsFor(source.Config).Fields;
        return fields.ContainsKey(name) || fields.ContainsKey(name + "?")
            ? null
            : ($"'{reference}' reads '{name}', which node '{source.Id.Value}' does not output", $"use one of: {string.Join(", ", fields.Keys.Order(StringComparer.Ordinal))}");
    }

    private static IEnumerable<(string Field, string Text)> Strings(JsonNode? node, string path) => node switch
    {
        JsonObject obj => obj.SelectMany(p => Strings(p.Value, path + "." + p.Key)),
        JsonArray list => list.SelectMany((item, i) => Strings(item, path + string.Create(CultureInfo.InvariantCulture, $"[{i}]"))),
        JsonValue value when value.TryGetValue<string>(out var text) => [(path, text)],
        _ => [],
    };

    private sealed record NodeScope(GraphNode Node, GraphStructure Structure, HashSet<string> Upstream);
}
