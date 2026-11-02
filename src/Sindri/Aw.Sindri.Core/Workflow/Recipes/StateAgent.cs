using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>
/// Which launcher agent a state's agent Step runs on (#1269). A bare id ('agent: codex') is <paramref name="Use"/>; '{ differentFrom: draft }' names an earlier state whose agent this one must differ
/// from. Exactly one is set (RCP-AGENT-003). A model is named in the Step's 'with.model', never here. Written into the hashed form only when the state has one.
/// </summary>
/// <param name="Use">The agent id this state runs on; null when it differs from a state instead.</param>
/// <param name="DifferentFrom">The state whose agent this state's agent must differ from (another agent when the catalog has one, else another model); null when it names an agent.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record StateAgent(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Use = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DifferentFrom = null);
