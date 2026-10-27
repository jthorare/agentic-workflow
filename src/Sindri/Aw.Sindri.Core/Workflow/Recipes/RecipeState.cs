using System.Text.Json.Serialization;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Core.Workflow.Recipes;

/// <summary>
/// A state (spec §3.4). One record for every kind, with the fields its kind allows (the schema rules check which):
/// an active state has <see cref="Step"/>, <see cref="Timeout"/> and <see cref="Next"/>, and may have
/// <see cref="Loop"/> and <see cref="Budget"/>; a wait state has <see cref="WaitFor"/>, <see cref="Timeout"/> and
/// <see cref="Next"/>, and may have <see cref="Loop"/>; a terminal state has <see cref="Outcome"/> and may have
/// <see cref="Next"/>.
/// </summary>
/// <param name="Kind">Active, wait or terminal.</param>
/// <param name="Step">The Step an active state runs.</param>
/// <param name="WaitFor">What a wait state waits for.</param>
/// <param name="Timeout">The state's deadline and what it does.</param>
/// <param name="Loop">The name of the loop (in the recipe's loops) this state belongs to.</param>
/// <param name="Budget">The state's token budget.</param>
/// <param name="Next">What the human reads next, with recipe variables (spec §3.7).</param>
/// <param name="Outcome">A terminal state's outcome: resolved, unresolved, alreadySatisfied or failed.</param>
/// <param name="Why">Optional plain words (at most 200 characters, no ids or code names; RCP-DISPLAY-002) on why this state is in the recipe; written into the hashed form only when set.</param>
/// <param name="DisplayName">Optional plain name (at most 60 characters; RCP-DISPLAY-003) the app shows for this step instead of the state id; written into the hashed form only when set.</param>
/// <param name="Model">What the state says about the model its agent Step runs on (model-pick spec 8): a floor tier; null for nothing. Written into the hashed form only when set.</param>
/// <param name="OnOutcome">Optional plain words for each outcome of a desk state, by outcome name ('approve: "Moves on to planning"'); each key is an outcome of the state's Step and each text plain words (RCP-DISPLAY-002); written into the hashed form only when set.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecipeState(
    StateKind Kind,
    StepUse? Step,
    WaitFor? WaitFor,
    TimeoutSpec? Timeout,
    string? Loop,
    BudgetSpec? Budget,
    string? Next,
    OutcomeName? Outcome,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Why = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? DisplayName = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyDictionary<string, string>? OnOutcome = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] StateModel? Model = null);
