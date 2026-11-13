using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Interpolation;
using Aw.Sindri.Core.Graph.Loops;

namespace Aw.Sindri.Core.Graph.Validation;

public sealed partial class GraphValidator
{
    private static readonly HashSet<string> _scopeRoots = new(["trigger", "item", "itemIndex", "workflow"], StringComparer.Ordinal);

    // why: the fields Scope.Root("workflow") builds in Aw.Sindri.Workflows/GraphNodeScope.cs; change both together
    private static readonly string[] _workflowFields = ["id", "name", "revision", "runId"];

    private void References(NodeScope scope, List<GraphFinding> found)
    {
        var seen = new HashSet<(string, string)>();
        foreach (var (field, text) in Strings(scope.Node.Config, "config").Where(f => !IsCodeBody(scope.Node, f.Field)))
        {
            if (Template.HasUnclosed(IsCondition(scope.Node, field) ? StringLiteral.Replace(text, "\"\"") : text))
            {
                found.Add(Found.At("GRAPH-REF-004", scope.Node.Id.Value, field, "a '{{' is never closed by '}}', so it would stay as literal text", "close the reference as {{node.field}}, or remove the '{{'"));
            }

            var refs = Read(scope.Node, field, text, found);
            var whole = WholeReference(text);
            var expected = whole is null ? null : FieldType(scope.Node, field);
            foreach (var reference in refs.Where(r => seen.Add((field, r))))
            {
                Reference(scope, field, reference, string.Equals(reference, whole, StringComparison.Ordinal) ? expected : null, found);
            }
        }
    }

    private static bool IsCondition(GraphNode node, string field) =>
        string.Equals(node.Type, "condition", StringComparison.Ordinal) && string.Equals(field, "config.when", StringComparison.Ordinal);

    // why: in a condition the parser knows string literals; Template would read a '{{y.z}}' inside one as a reference
    private IReadOnlyList<string> Read(GraphNode node, string field, string text, List<GraphFinding> found) =>
        IsCondition(node, field)
            ? Condition(node, field, text, found)
            : Template.References(text);

    private IReadOnlyList<string> Condition(GraphNode node, string field, string text, List<GraphFinding> found)
    {
        var parsed = context.Condition(text);
        if (parsed.Error is { } error)
        {
            found.Add(Found.At("GRAPH-COND-001", node.Id.Value, field, $"the condition can't be read: {error}", "write it with references, comparisons, and/or/not and exists(x)"));
        }

        return parsed.References;
    }

    private void Reference(NodeScope scope, string field, string reference, BlockFieldType? expected, List<GraphFinding> found)
    {
        var id = scope.Node.Id.Value;
        var root = reference.Split('.', 2)[0];
        if (string.Equals(root, "trigger", StringComparison.Ordinal))
        {
            TriggerReference(new RefSite(scope, field, reference, expected), found);
            return;
        }

        if (_scopeRoots.Contains(root))
        {
            ScopeReference(scope, field, reference, found);
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
        else if (Unreachable(scope, root, reference) is { } unreachable)
        {
            found.Add(Found.At("GRAPH-REF-001", id, field, unreachable.Message, unreachable.Fix));
        }
        else if (OutputProblem(scope.Structure.Find(root), reference) is { } problem)
        {
            found.Add(Found.At("GRAPH-REF-002", id, field, problem.Message, problem.Fix));
        }
        else
        {
            TypeProblem(new RefSite(scope, field, reference, expected), scope.Structure.Find(root), found);
        }
    }

    private static void ScopeReference(NodeScope scope, string field, string reference, List<GraphFinding> found)
    {
        var id = scope.Node.Id.Value;
        var root = reference.Split('.', 2)[0];
        if ((string.Equals(root, "item", StringComparison.Ordinal) || string.Equals(root, "itemIndex", StringComparison.Ordinal)) && !scope.Loops.Exists(l => l.Repeats(scope.Node.Id)))
        {
            found.Add(Found.At("GRAPH-REF-001", id, field, $"'{reference}' reads '{root}', but '{id}' is not inside a for_each loop, so there is no item to read", $"use '{root}' only in nodes between a for_each and its collect"));
        }

        if (string.Equals(root, "workflow", StringComparison.Ordinal) && reference.Split('.') is [_, var name, ..] && !_workflowFields.Contains(name, StringComparer.Ordinal))
        {
            found.Add(Found.At("GRAPH-REF-002", id, field, $"'{reference}' reads '{name}', which a workflow run does not have", $"use one of: {string.Join(", ", _workflowFields)}"));
        }
    }

    // why: 'upstream' only says a path exists; a value is there only when the node has run every time the reader runs (GraphStructure.RunsWhenever) and, for a loop body node, the reader is in the same body
    private static (string Message, string Fix)? Unreachable(NodeScope scope, string root, string reference)
    {
        var id = scope.Node.Id.Value;
        if (scope.Loops.Find(l => l.Body.Any(b => string.Equals(b.Id.Value, root, StringComparison.Ordinal))) is { } loop && !loop.Repeats(scope.Node.Id))
        {
            var each = loop.ForEach.Id.Value;
            var gather = loop.Collect.Id.Value;
            return ($"'{reference}' reads node '{root}', which runs inside the for_each '{each}', so it has no single value in '{id}'", $"read '{gather}' instead");
        }

        return scope.Structure.RunsWhenever(id, root)
            ? null
            : ($"'{reference}' reads node '{root}', which does not run on every path to '{id}' (a path to '{id}' skips it)", "read it inside the branch it runs on, or join the branches first");
    }

    private void TriggerReference(RefSite site, List<GraphFinding> found)
    {
        if (!site.Reference.Contains('.', StringComparison.Ordinal) || TriggerNode(site.Scope.Structure) is not { } trigger)
        {
            return;
        }

        if (OutputProblem(trigger, site.Reference) is { } missing)
        {
            found.Add(Found.At("GRAPH-REF-002", site.Scope.Node.Id.Value, site.Field, missing.Message, missing.Fix));
        }
        else
        {
            TypeProblem(site, trigger, found);
        }
    }

    private GraphNode? TriggerNode(GraphStructure structure) =>
        structure.Order.Select(structure.Find).FirstOrDefault(n => n is not null && context.Blocks.TryGetValue(n.Type, out var block) && block.IsTrigger);

    // why: a reference that is the whole value keeps its type at run time, so it has to fit the field it fills
    private void TypeProblem(RefSite site, GraphNode? source, List<GraphFinding> found)
    {
        var (scope, field, reference, expected) = site;
        if (expected is not { } want || source is null || !context.Blocks.TryGetValue(source.Type, out var block) || reference.Split('.') is not [_, var name, ..])
        {
            return;
        }

        var fields = block.OutputsFor(source.Config).Fields;
        var have = fields.TryGetValue(name, out var declared) ? declared : fields.GetValueOrDefault(name + "?")?.TrimEnd('?');
        var need = Needed(want);
        if (Fits(have, need))
        {
            return;
        }

        found.Add(Found.At("GRAPH-REF-005", scope.Node.Id.Value, field, $"'{reference}' is {have.TrimEnd('?')}, but this field takes {need}", $"read an output of type {need}, or write the value in place"));
    }

    // why: a block declares 'any' for an output whose type depends on the answer (an http body), which fits every field
    private static bool Fits([NotNullWhen(false)] string? have, [NotNullWhen(false)] string? need) =>
        have is null || need is null || string.Equals(have.TrimEnd('?'), "any", StringComparison.Ordinal) || string.Equals(have.TrimEnd('?'), need, StringComparison.Ordinal);

    private static string? Needed(BlockFieldType type) => type switch
    {
        BlockFieldType.Number => "number",
        BlockFieldType.Boolean => "boolean",
        BlockFieldType.TextList => "array",
        BlockFieldType.Map => "object",
        BlockFieldType.Text => null,
        _ => null,
    };

    private BlockFieldType? FieldType(GraphNode node, string field)
    {
        var name = field.StartsWith("config.", StringComparison.Ordinal) ? field["config.".Length..] : string.Empty;
        return context.Blocks.TryGetValue(node.Type, out var block) && block.Config.Fields.FirstOrDefault(f => string.Equals(f.Name, name, StringComparison.Ordinal)) is { } found
            ? found.Type
            : null;
    }

    private static string? WholeReference(string text)
    {
        var refs = Template.References(text);
        return refs.Count == 1 && Template.Render(text, static r => JsonValue.Create(r)) is JsonValue value && value.GetValueKind() == JsonValueKind.String && string.Equals(value.GetValue<string>(), refs[0], StringComparison.Ordinal) ? refs[0] : null;
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

    private sealed record RefSite(NodeScope Scope, string Field, string Reference, BlockFieldType? Expected);

    private sealed record NodeScope(GraphNode Node, GraphStructure Structure, HashSet<string> Upstream, List<LoopShape> Loops);

    // why: a double-quoted literal of a condition (with backslash escapes) is text, so a '{{' inside it is not an unclosed reference
    [GeneratedRegex("\"(?:\\\\.|[^\"\\\\])*\"", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex StringLiteral { get; }
}
