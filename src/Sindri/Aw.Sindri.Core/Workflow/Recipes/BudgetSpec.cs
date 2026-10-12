using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>A token budget (spec §3.7), for the recipe or for an active state; exhausting it parks with 'budget'.</summary>
/// <param name="Tokens">The tokens it allows, at least 1 (the reader reads '200k' with <see cref="TokenCounts.TryParse"/>).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BudgetSpec(int Tokens);
