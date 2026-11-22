using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The profile's 'workflow.budget:' block (#3805): the token budget of a workflow run whose spec sets no 'budget'.</summary>
/// <param name="Tokens">The most tokens one run's 'prompt' and 'run_agent' nodes may use, a whole number above 0; absent: no cap.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RunBudgetSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? Tokens = null);
