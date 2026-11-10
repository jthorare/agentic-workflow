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
    /// <summary>Gets the workflow's provisioned resources by environment name ('SINDRI_DB_STATE' is the path of the database 'state'); empty when it declares none (agent-first PR 14).</summary>
    public IReadOnlyDictionary<string, string> Resources { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
}
