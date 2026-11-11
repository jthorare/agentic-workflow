using System.Globalization;
using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Validation;

public sealed partial class GraphValidator
{
    // why: a typo in a 'code' node's C# fails at save time, with the line and column of the node's own body (agent-first PR 9c)
    private void CodeBody(GraphNode node, List<GraphFinding> found)
    {
        if (context.CodeCheck is not { } compile || node.Config["code"] is not JsonValue value || !value.TryGetValue<string>(out var body))
        {
            return;
        }

        foreach (var problem in compile(body))
        {
            found.Add(Found.At("GRAPH-CODE-001", node.Id.Value, "config.code", string.Create(CultureInfo.InvariantCulture, $"the code does not compile: line {problem.Line}, column {problem.Column}: {problem.Rule} {problem.Message}"), "fix the C# at that line and column; the body is a method that gets 'input' and returns a value"));
        }
    }

    // why: the C# of a 'code' node is not a template, so a '{{' in it (an interpolated string) is not a reference
    private static bool IsCodeBody(GraphNode node, string field) =>
        string.Equals(node.Type, "code", StringComparison.Ordinal) && string.Equals(field, "config.code", StringComparison.Ordinal);
}
