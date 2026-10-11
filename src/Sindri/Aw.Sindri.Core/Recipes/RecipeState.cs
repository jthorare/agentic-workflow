using System.Text.Json.Serialization;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Recipes;

/// <summary>One state of a recipe.</summary>
/// <param name="Kind">active, wait or terminal.</param>
/// <param name="Step">The step block that works in an active state.</param>
/// <param name="Timeout">How long the state may last ('30s', '10m', '4h', '2d'); required for active and wait states.</param>
/// <param name="OnTimeout">What the timeout does (default park).</param>
/// <param name="Budget">The state's own budget, reset each time it is entered.</param>
/// <param name="Child">A wait state may start a child workflow of this recipe and wait for its terminal event ('child.done').</param>
/// <param name="Next">The text status and resume show for this state: the one allowed next step.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecipeState(
    StateKind Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] BlockId? Step = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Timeout = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] TimeoutAction? OnTimeout = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] BudgetSpec? Budget = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] RecipeId? Child = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Next = null);
