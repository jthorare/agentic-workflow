using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What a reference can read in one run: the trigger, the workflow's own facts, the item inside a loop and the outputs of the nodes that succeeded.</summary>
internal sealed class GraphNodeScope(WorkflowRun run, GraphSpec spec, IReadOnlyList<NodeRun> nodeRuns, GraphLoopContext? loop = null)
{
    private readonly JsonObject? _trigger = JsonNode.Parse(run.Trigger) as JsonObject;

    /// <summary>The node whose cut output a reference read, or null.</summary>
    public string? Cut { get; private set; }

    /// <summary>The node whose output the redactor blocked and a reference read, or null.</summary>
    public string? Blocked { get; private set; }

    /// <summary>The first reference that read a node that failed and continued, as the node id and the reference text; null when none did.</summary>
    public (string Node, string Reference)? ContinuedFailure { get; private set; }

    public JsonNode? Lookup(string reference)
    {
        var parts = reference.Split('.');
        var (current, failed) = Root(parts[0]);
        if (failed is not null)
        {
            ContinuedFailure ??= (failed, reference);   // why: a failed row exists only for a node whose onFailure is 'continue' (any other failure ends the walk), so reading it is reading a continued failure
        }

        foreach (var part in parts.Skip(1))
        {
            current = current is JsonObject o && o.TryGetPropertyValue(part, out var next) ? next : null;
        }

        return current?.DeepClone();
    }

    /// <summary>The value of a reference's first segment, and the node it read when that node failed and continued (null otherwise).</summary>
    private (JsonNode? Value, string? FailedNode) Root(string name) => name switch
    {
        "trigger" => (_trigger, null),
        // why: GraphValidator._workflowFields lists these four names; change both together
        "workflow" => (new JsonObject { ["name"] = spec.Name, ["runId"] = run.Id.Value, ["revision"] = run.Revision, ["id"] = run.WorkflowId.Value }, null),
        "item" => (loop?.Item, null),
        "itemIndex" => (loop is null ? null : JsonValue.Create(loop.ItemIndex), null),
        _ => Output(name),
    };

    private (JsonNode? Value, string? FailedNode) Output(string node)
    {
        var index = IndexOf(node);
        var found = RowAt(node, index, NodeRunStatus.Succeeded);
        if (found is null)
        {
            return (null, RowAt(node, index, NodeRunStatus.Failed) is null ? null : node);
        }

        if (found.Truncated)   // why: Fit's cut sets 'Truncated' on the row; the output's shape is never read, so a real output shaped like the cut envelope is a value (#3486)
        {
            Cut ??= node;
            return (null, null);
        }

        var output = found.Output is null ? null : JsonNode.Parse(found.Output);
        if (StoredJson.IsBlocked(output))
        {
            Blocked ??= node;
            return (null, null);
        }

        return (output, null);
    }

    // why: a node of the loop's body has one row per item and is read at this item's; every other node has its one row at index 0
    private int IndexOf(string node) =>
        loop is not null && loop.Shape.Body.Any(b => string.Equals(b.Id.Value, node, StringComparison.Ordinal)) ? loop.ItemIndex : 0;

    private NodeRun? RowAt(string node, int index, NodeRunStatus status) =>
        nodeRuns.FirstOrDefault(n => string.Equals(n.NodeId.Value, node, StringComparison.Ordinal) && n.ItemIndex == index && n.Status == status);
}
