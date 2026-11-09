using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What a reference can read in one run: the trigger, the workflow's own facts and the outputs of the nodes that succeeded.</summary>
internal sealed class GraphNodeScope(WorkflowRun run, GraphSpec spec, IReadOnlyList<NodeRun> nodeRuns)
{
    private readonly JsonObject? _trigger = JsonNode.Parse(run.Trigger) as JsonObject;

    /// <summary>The node whose cut output a reference read, or null.</summary>
    public string? Cut { get; private set; }

    public JsonNode? Lookup(string reference)
    {
        var parts = reference.Split('.');
        var current = Root(parts[0]);
        foreach (var part in parts.Skip(1))
        {
            current = current is JsonObject o && o.TryGetPropertyValue(part, out var next) ? next : null;
        }

        return current?.DeepClone();
    }

    private JsonNode? Root(string name) => name switch
    {
        "trigger" => _trigger,
        "workflow" => new JsonObject { ["name"] = spec.Name, ["runId"] = run.Id.Value, ["revision"] = run.Revision },
        _ => Output(name),
    };

    private JsonNode? Output(string node)
    {
        var found = nodeRuns.FirstOrDefault(n => string.Equals(n.NodeId.Value, node, StringComparison.Ordinal) && n.ItemIndex == 0 && n.Status == NodeRunStatus.Succeeded);
        if (found is null)
        {
            return null;
        }

        if (found.Truncated)
        {
            Cut ??= node;
            return null;
        }

        return found.Output is null ? null : JsonNode.Parse(found.Output);
    }
}
