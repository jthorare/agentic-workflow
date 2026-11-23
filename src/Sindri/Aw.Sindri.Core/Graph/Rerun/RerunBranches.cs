using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph.Runs;

namespace Aw.Sindri.Core.Graph.Rerun;

/// <summary>
/// Reads the branch a step took from its own stored row (<see cref="RerunPlan.Branches"/>). Only two block types leave by named branches: a condition stores
/// <c>{"result": bool}</c> and an ask_owner stores who answered, 'timeout' when nobody did (ConditionBlock, AskOwnerBlock.Resolve); every other step leaves by 'out'.
/// </summary>
internal static class RerunBranches
{
    // why: the block type names, not the block classes: Graph.Rerun stays a leaf namespace and never reaches Graph.Blocks
    private const string Condition = "condition";
    private const string AskOwner = "ask_owner";
    private const string Timeout = "timeout";

    internal static IReadOnlyList<string>? Of(GraphNode node, IReadOnlyList<NodeRun> rows)
    {
        if (rows.FirstOrDefault(static r => r.Status == NodeRunStatus.Succeeded) is not { } row)
        {
            return [];
        }

        if (string.Equals(node.Type, Condition, StringComparison.Ordinal))
        {
            return OfCondition(row.Output);
        }

        return string.Equals(node.Type, AskOwner, StringComparison.Ordinal) ? OfAskOwner(row.Output) : ["out"];
    }

    private static List<string>? OfCondition(string? output)
    {
        if (Parse(output)?["result"] is JsonValue value && value.TryGetValue<bool>(out var result))
        {
            return [result ? "true" : "false"];
        }

        return null;
    }

    private static List<string>? OfAskOwner(string? output)
    {
        if (Parse(output)?["answeredBy"] is JsonValue value && value.TryGetValue<string>(out var by))
        {
            return [string.Equals(by, Timeout, StringComparison.Ordinal) ? Timeout : "answered"];
        }

        return null;
    }

    private static JsonObject? Parse(string? output)
    {
        if (output is null)
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(output) as JsonObject;
        }
        catch (JsonException)   // boundary: a stored output that is not JSON says no branch
        {
            return null;
        }
    }
}
