using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>Builds long-form recipes for tests.</summary>
public static class ARecipe
{
    /// <summary>
    /// The smallest valid long form: one desk Step 'produce' (step.produce, a 4h park timeout) that goes to the
    /// terminal 'done' (resolved) on outcome 'done'.
    /// </summary>
    /// <param name="id">The recipe's id.</param>
    /// <returns>The recipe.</returns>
    public static Recipe Minimal(RecipeId id) => new(
        Schema: 1,
        Id: id,
        Version: 1,
        Description: null,
        Base: null,
        Inputs: [],
        Initial: "produce",
        States: new Dictionary<string, RecipeState>(StringComparer.Ordinal)
        {
            ["produce"] = new(StateKind.Active, new StepUse(BlockId.From("step.produce"), NoArguments, []), WaitFor: null,
                new TimeoutSpec(RecipeDuration.From("4h"), TimeoutAction.Park), Loop: null, Budget: null, Next: "Produce the Step output.", Outcome: null),
            ["done"] = new(StateKind.Terminal, Step: null, WaitFor: null, Timeout: null, Loop: null, Budget: null, Next: null, OutcomeName.From("resolved")),
        },
        Transitions:
        [
            new(["produce"], "done", "step.completed", When(OutcomeName.From("done")), [], [], [], []),
        ],
        Loops: new Dictionary<string, LoopSpec>(StringComparer.Ordinal),
        Budget: null,
        Evals: [],
        Hash: null);

    /// <summary>A '{ outcome }' condition.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <returns>The condition.</returns>
    public static WhenClause When(OutcomeName outcome) =>
        new(outcome, Fact: null, Is: null, Loop: null, Below: null, All: null, Any: null, Not: null);

    private static Dictionary<string, WithValue> NoArguments => new(StringComparer.Ordinal);
}
