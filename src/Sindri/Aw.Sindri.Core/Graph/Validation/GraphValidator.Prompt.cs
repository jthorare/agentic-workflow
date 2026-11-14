using System.Text.Json.Nodes;

namespace Aw.Sindri.Core.Graph.Validation;

public sealed partial class GraphValidator
{
    // why: a prompt that reads no data and sits right after the trigger asks the model about nothing, so it can only guess (#3509)
    private void PromptReads(GraphSpec spec, GraphStructure structure, List<GraphFinding> found)
    {
        foreach (var node in spec.Nodes.Where(static n => string.Equals(n.Type, "prompt", StringComparison.Ordinal)))
        {
            var id = node.Id.Value;
            if (node.Config["prompt"] is not JsonValue value || !value.TryGetValue<string>(out var question) || question.Contains("{{", StringComparison.Ordinal))
            {
                continue;
            }

            var parents = spec.Edges.Where(e => string.Equals(e.To.Value, id, StringComparison.Ordinal)).Select(static e => e.From.Value).Distinct(StringComparer.Ordinal).ToList();
            if (parents.Count > 0 && parents.TrueForAll(p => structure.Find(p) is { } parent && IsTriggerType(parent.Type)))
            {
                found.Add(new GraphFinding("GRAPH-PROMPT-001", GraphSeverity.Warning, [id], "config.prompt", $"prompt node '{id}' reads no data: its question has no {{{{...}}}} reference and only the trigger runs before it, so the model answers about nothing", $"add a step before '{id}' that fetches the data, then reference its output in the question, such as {{{{step.field}}}}"));
            }
        }
    }
}
