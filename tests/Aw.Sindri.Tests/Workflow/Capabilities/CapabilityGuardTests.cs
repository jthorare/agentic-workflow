using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Capabilities.CapabilityRecipe;

namespace Aw.Sindri.Tests.Workflow.Capabilities;

/// <summary>
/// RCP-CAP-005 (spec §4.3, W10): for each kept capability use, the guards that dominate it (the gates and evidence on
/// every path from initial to it, plus its own) are a superset of the base's, compared by path. In the base,
/// gate.review (0.8) and evidence tests@head guard produce -> publish, and gate.check guards publish -> running, so
/// effect.repo.push on publish -> running and the child in 'running' are guarded by all three.
/// </summary>
public sealed class CapabilityGuardTests
{
    private const string Fix = "keep the base's gates and evidence on every path to it";

    [Fact, Trait("Lane", "unit")]
    public void A_lower_threshold_on_a_dominating_gate_is_refused()
    {
        // The recipe of invalid/RCP-CAP-005.yaml: 'running' waits for an answer instead of the base's child (a use
        // fewer, which the base allows), so the lowered gate guards one capability use, the effect at 47:9.
        var recipe = (Generated with { Id = RecipeId.From("workflow.cap-005") })
            .WithTransition(0, t => t with { Gates = [Gate("gate.review", 0.6), Gate("gate.check")] })
            .WithState("running", s => s with
            {
                WaitFor = new WaitFor(Event: null, new AnswerWait(AnswerId.From("go"), "Publish it?"), Resource: null, Child: null),
                Next = "Waiting for the answer.",
            });

        Assert.Equal(
            ["RCP-CAP-005: capability use effect.repo.push on publish -> running is guarded by gate.review at threshold 0.6, below base workflow.cap-base's 0.8 at RCP-CAP-005.yaml:47:9 (transitions[2].effects[0].use); raise the threshold to 0.8 or more"],
            Lines(recipe, Base, "RCP-CAP-005.yaml", [("transitions[2].effects[0].use", 47, 9)]));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_lower_threshold_is_reported_for_each_use_it_guards()
    {
        var recipe = Generated.WithTransition(0, t => t with { Gates = [Gate("gate.review", 0.6), Gate("gate.check")] });

        Assert.Equal(
            [
                "RCP-CAP-005: capability use child workflow.step in state 'running' is guarded by gate.review at threshold 0.6, below base workflow.cap-base's 0.8 at r.yaml:1:1 (states.running.waitFor.child.recipe); raise the threshold to 0.8 or more",
                "RCP-CAP-005: capability use effect.repo.push on publish -> running is guarded by gate.review at threshold 0.6, below base workflow.cap-base's 0.8 at r.yaml:1:1 (transitions[2].effects[0].use); raise the threshold to 0.8 or more",
            ],
            Lines(recipe, Base));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_use_that_loses_its_own_gate_is_refused()
    {
        var recipe = Generated
            .WithTransition(0, t => t with { Gates = [Gate("gate.review", 0.9)] })
            .WithTransition(2, t => t with { Gates = [] });

        Assert.Equal(
            [
                $"RCP-CAP-005: capability use child workflow.step in state 'running' lost gate gate.check, which guards it in base workflow.cap-base at r.yaml:1:1 (states.running.waitFor.child.recipe); {Fix}",
                $"RCP-CAP-005: capability use effect.repo.push on publish -> running lost gate gate.check, which guards it in base workflow.cap-base at r.yaml:1:1 (transitions[2].effects[0].use); {Fix}",
            ],
            Lines(recipe, Base));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_use_that_loses_dominating_evidence_is_refused()
    {
        var recipe = Generated.WithTransition(0, t => t with { Evidence = [] });

        Assert.Equal(
            [
                $"RCP-CAP-005: capability use child workflow.step in state 'running' lost evidence tests at head expecting pass, which guards it in base workflow.cap-base at r.yaml:1:1 (states.running.waitFor.child.recipe); {Fix}",
                $"RCP-CAP-005: capability use effect.repo.push on publish -> running lost evidence tests at head expecting pass, which guards it in base workflow.cap-base at r.yaml:1:1 (transitions[2].effects[0].use); {Fix}",
            ],
            Lines(recipe, Base));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_gate_that_holds_on_only_some_paths_does_not_guard()
    {
        // A second way into 'publish', without gate.review or the evidence.
        var recipe = Generated with { Transitions = [.. Generated.Transitions, Go("produce", "publish", "command.skip")] };

        Assert.Equal(
            [
                $"RCP-CAP-005: capability use child workflow.step in state 'running' lost gate gate.review, which guards it in base workflow.cap-base at r.yaml:1:1 (states.running.waitFor.child.recipe); {Fix}",
                $"RCP-CAP-005: capability use child workflow.step in state 'running' lost evidence tests at head expecting pass, which guards it in base workflow.cap-base at r.yaml:1:1 (states.running.waitFor.child.recipe); {Fix}",
                $"RCP-CAP-005: capability use effect.repo.push on publish -> running lost gate gate.review, which guards it in base workflow.cap-base at r.yaml:1:1 (transitions[2].effects[0].use); {Fix}",
                $"RCP-CAP-005: capability use effect.repo.push on publish -> running lost evidence tests at head expecting pass, which guards it in base workflow.cap-base at r.yaml:1:1 (transitions[2].effects[0].use); {Fix}",
            ],
            Lines(recipe, Base));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_gate_with_other_arguments_is_another_gate()
    {
        var review = Gate("gate.review", 0.9) with
        {
            With = new Dictionary<string, WithValue>(StringComparer.Ordinal) { ["question"] = Literal("other") },
        };
        var recipe = Generated.WithTransition(0, t => t with { Gates = [review, Gate("gate.check")] });

        Assert.Equal(
            [
                $"RCP-CAP-005: capability use child workflow.step in state 'running' lost gate gate.review, which guards it in base workflow.cap-base at r.yaml:1:1 (states.running.waitFor.child.recipe); {Fix}",
                $"RCP-CAP-005: capability use effect.repo.push on publish -> running lost gate gate.review, which guards it in base workflow.cap-base at r.yaml:1:1 (transitions[2].effects[0].use); {Fix}",
            ],
            Lines(recipe, Base));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_guard_moved_earlier_on_every_path_still_guards()
    {
        // The generated recipe already has gate.check on produce -> publish, which every path to the effect crosses.
        var recipe = Generated.WithTransition(2, t => t with { Gates = [] });

        Assert.Empty(Lines(recipe, Base));
    }
}
