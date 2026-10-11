using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Recipes;

/// <summary>A budget in tokens and turns (design spec §6); exhaustion parks the instance with the reason 'budget'.</summary>
/// <param name="Tokens">The token cap, or null for none.</param>
/// <param name="Turns">The turn cap, or null for none.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BudgetSpec(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? Tokens = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Turns = null);
