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
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RecipeState(
    StateKind Kind,
    StepUse? Step,
    WaitFor? WaitFor,
    TimeoutSpec? Timeout,
    string? Loop,
    BudgetSpec? Budget,
    string? Next,
    OutcomeName? Outcome);
