using System.Text.Json.Nodes;
using Aw.Sindri.Core.Graph.Ids;

namespace Aw.Sindri.Core.Graph.Blocks;

/// <summary>What one node execution is given.</summary>
/// <param name="RunId">The run's id, '&lt;workflow&gt;@&lt;revision&gt;:&lt;occurrence&gt;'.</param>
/// <param name="Node">The node being run.</param>
/// <param name="Config">The node's config with every '{{ref}}' replaced by its value.</param>
/// <param name="RawConfig">The config as written, for a block that reads references itself (a 'condition' evaluates its 'when' against <paramref name="Resolve"/>).</param>
/// <param name="Resolve">Reads a reference such as 'search.facts' or 'trigger.scheduledFor'; 'trigger' alone is the whole trigger payload. Null for a missing value.</param>
public sealed record BlockContext(WorkflowRunId RunId, NodeId Node, JsonObject Config, JsonObject RawConfig, Func<string, JsonNode?> Resolve)
{
    /// <summary>The item's position inside a 'for_each' body (counted from 1), or null for a node outside a loop.</summary>
    public int? Item { get; init; }

    /// <summary>The id of the workflow whose activity runs the block; set for a loop item (its batch's child workflow), null otherwise.</summary>
    public string? BatchWorkflow { get; init; }

    /// <summary>Gets the workflow's provisioned resources by environment name ('SINDRI_DB_STATE' is the path of the database 'state'); empty when it declares none (agent-first PR 14).</summary>
    public IReadOnlyDictionary<string, string> Resources { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets what a failing block left for the run page, or null; the engine stores it as the node's output when the block returns an error.</summary>
    public JsonObject? FailureOutput { get; private set; }

    /// <summary>Leaves the output the engine stores when this block then returns an error (a prompt step leaves the model's raw answer); the engine cuts it to the stored-output cap.</summary>
    /// <param name="output">The fields to show on the failed step.</param>
    public void LeaveFailureOutput(JsonObject output) => FailureOutput = output;

    /// <summary>Gets the model text a failing prompt block left for the run page's Raw answer tab, or null (P3e, spec §6.2); the engine scrubs it with Aw.Redaction and cuts it at 64 KB before it is stored.</summary>
    public string? Raw { get; private set; }

    /// <summary>Leaves the model's text for the node run's raw.</summary>
    /// <param name="text">The text the model answered.</param>
    public void LeaveRaw(string text) => Raw = text;
}
