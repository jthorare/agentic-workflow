using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Graph;

/// <summary>A workflow's token budget ('budget' in the spec, #3805): the most tokens one run's 'prompt' and 'run_agent' nodes may use before the next such node is refused.</summary>
/// <param name="Tokens">The cap, a whole number above 0 (GRAPH-BUDGET-001 otherwise); null is refused the same way.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record GraphBudget(long? Tokens = null);
