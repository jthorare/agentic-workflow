using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// A recipe trigger on a suspended instance (engine spec §3.8, §5.2; #2071): a parked or taken-over instance refuses it with SND-WF-003
/// because it is suspended, even when a recipe has a state named like the suspension ('parked', 'taken-over') so that
/// <see cref="WorkflowInstance.State"/> names a recipe state with a matching transition.
/// </summary>
public sealed class SuspendedTriggerTests
{
    [Fact, Trait("Lane", "unit")]
    public void A_step_report_on_a_parked_instance_is_refused_even_when_State_names_a_recipe_state()
    {
        var recipe = Named(EngineStates.Parked);
        var park = new Suspension(
            SuspensionKind.Park, ParkReason.NeedsAnswer, ParkKind.Step, Escalation: null, "q", "human:thor", Mirrors: null,
            new ResumeAction(ResumeKind.Rerun, Withheld: null, Gate: null, Effects: []));
        var instance = At(recipe, EngineStates.Parked) with { Suspension = park };

        var decision = Decide(instance, recipe, Completed(1, "done", ("fact:output", "x")));

        Assert.Equal(EngineStates.Parked, instance.State);
        Assert.Equal("SND-WF-003", Assert.IsType<Rejected>(decision).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_step_report_on_a_taken_over_instance_is_refused_even_when_State_names_a_recipe_state()
    {
        var recipe = Named(EngineStates.TakenOver);
        var takeover = new Suspension(
            SuspensionKind.Takeover, Reason: null, ParkKind: null, Escalation: null, Question: string.Empty, "human:thor", Mirrors: null,
            new ResumeAction(ResumeKind.Return, Withheld: null, Gate: null, Effects: []));
        var instance = At(recipe, EngineStates.TakenOver) with { Suspension = takeover };

        var decision = Decide(instance, recipe, Completed(1, "done", ("fact:output", "x")));

        Assert.Equal(EngineStates.TakenOver, instance.State);
        Assert.Equal("SND-WF-003", Assert.IsType<Rejected>(decision).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_command_on_a_parked_instance_is_refused_even_when_State_names_a_recipe_state()
    {
        var recipe = Named(EngineStates.Parked);
        recipe = recipe with { Transitions = [.. recipe.Transitions, recipe.Transitions[0] with { On = "command.ship", When = null }] };
        var park = new Suspension(
            SuspensionKind.Park, ParkReason.NeedsAnswer, ParkKind.Step, Escalation: null, "q", "human:thor", Mirrors: null,
            new ResumeAction(ResumeKind.Rerun, Withheld: null, Gate: null, Effects: []));
        var instance = At(recipe, EngineStates.Parked) with { Suspension = park };
        var ship = new CommandFired("human:thor", CommandVerb.From("ship"), new CommandStamp(CommandNonce.From(new string('a', 32)), Sha("ship")));

        var decision = Decide(instance, recipe, ship);

        Assert.Equal("SND-WF-003", Assert.IsType<Rejected>(decision).Error.Code);
    }

    /// <summary>The minimal recipe with its active state renamed <paramref name="name"/> (initial and transition included).</summary>
    private static Recipe Named(string name)
    {
        var recipe = ARecipe.Minimal(RecipeId.From("workflow.test"));
        var states = new Dictionary<string, RecipeState>(recipe.States, StringComparer.Ordinal) { [name] = recipe.States["produce"] };
        _ = states.Remove("produce");
        return recipe with
        {
            Initial = name,
            States = states,
            Transitions = [recipe.Transitions[0] with { From = [name] }],
        };
    }
}
