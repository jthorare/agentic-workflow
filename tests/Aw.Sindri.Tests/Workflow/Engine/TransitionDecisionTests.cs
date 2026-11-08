using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>Deciding a recipe transition (spec §3.5, §5.1–§5.3, §5.7, W5, W20), over the shipped workflow.step and workflow.item.</summary>
public sealed class TransitionDecisionTests
{
    [Fact, Trait("Lane", "unit")]
    public void Late_result_from_an_earlier_entry_is_dropped()
    {
        var checks = At(Step, "checks", entry: 2);
        var late = Completed(1, "done", ("fact:output", "Added --version."));

        var decision = Decide(checks, Step, late);

        Assert.Equal(MatchMiss.StaleRun, TransitionDecision.Match(checks, Step, late).Miss);
        Assert.Equal("SND-WF-005", Assert.IsType<Rejected>(decision).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void Stale_token_report_records_nothing()
    {
        var rerun = At(Step, "produce", entry: 1, run: 2);

        var decision = Decide(rerun, Step, Completed(1, 1, "done", ("fact:output", "Added --version.")));

        var rejected = Assert.IsType<Rejected>(decision);
        Assert.Equal("SND-WF-005", rejected.Error.Code);
        Assert.Contains("1.2", rejected.Error.Message, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("failed", "fact:output")]
    [InlineData("done", null)]
    [InlineData("done", "fact:findings")]
    public void Malformed_report_is_rejected_without_a_round(string outcome, string? output)
    {
        (string, string)[] outputs = output is null ? [] : output == "fact:findings" ? [("fact:output", "x"), (output, "y")] : [(output, "x")];

        var decision = Decide(At(Step, "produce"), Step, Completed(1, outcome, outputs));

        Assert.Equal("SND-WF-008", Assert.IsType<Rejected>(decision).Error.Code);
    }

    private const string DeskDecision = """{"choice":"api","provider":"desk","actor":"human:test"}""";

    [Fact, Trait("Lane", "unit")]
    public void A_desk_report_of_a_choice_is_accepted()
    {
        var route = EvalComposition.Recipe(Choice.RouteRecipes.Dev);

        var decision = Decide(At(route, "route-evidence-desk"), route, Completed(1, "api", ("fact:decision", DeskDecision)));

        var applied = Assert.IsType<Applied>(decision);
        Assert.Equal("evidence-api", applied.Changes[0].After.State);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_report_outside_the_choices_is_SND_WF_008_listing_them()
    {
        var route = EvalComposition.Recipe(Choice.RouteRecipes.Dev);

        var twin = Assert.IsType<Rejected>(Decide(At(route, "route-evidence-desk"), route, Completed(1, "sideways", ("fact:decision", DeskDecision))));
        var judge = Assert.IsType<Rejected>(Decide(At(route, "route-evidence"), route, Completed(1, "sideways", ("fact:decision", DeskDecision))));

        Assert.Equal("SND-WF-008", twin.Error.Code);
        Assert.Contains("use one of ui, api, db, all, none, other)", twin.Error.Message, StringComparison.Ordinal);
        Assert.Contains("use one of ui, api, db, all, none, other, uncertain)", judge.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_choice_report_without_fact_decision_is_SND_WF_008()
    {
        var route = EvalComposition.Recipe(Choice.RouteRecipes.Dev);

        var rejected = Assert.IsType<Rejected>(Decide(At(route, "route-evidence-desk"), route, Completed(1, "api")));

        Assert.Equal("SND-WF-008", rejected.Error.Code);
        Assert.Contains("without fact:decision", rejected.Error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("include the required output", rejected.Error.Message, StringComparison.Ordinal);   // CompleteStep refuses a hand-written engine-filled output
        Assert.Contains("retry 'workflow complete' with the same --nonce", rejected.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Step_without_runtime_and_not_desk_capable_parks()
    {
        var recipe = WithAgentReview();

        var applied = Assert.IsType<Applied>(Decide(At(recipe, "produce"), recipe, Completed(1, "done", ("fact:output", "x"))));

        var after = applied.Changes[0].After;
        Assert.Equal(EngineStates.Parked, after.State);
        Assert.Equal(ParkReason.NoRuntime, after.Park);
        Assert.Equal(("review", 2), (after.Frame.State, after.Frame.Entry));
        Assert.Equal(ResumeKind.Restart, after.Suspension?.Resume.Kind);
        Assert.Empty(applied.FollowUps);
    }

    [Fact, Trait("Lane", "unit")]
    public void Answer_SND_WF_010_runs_restart()
    {
        var recipe = WithAgentReview();
        var real = EngineRules.Default;
        var parked = Assert.IsType<Applied>(Decide(At(recipe, "produce"), recipe, Completed(1, "done", ("fact:output", "x")), rules: real)).Changes[0].After;
        var resume = new AnswerGiven("human:cli", new CommandStamp(CommandNonce.From(new string('c', 32)), Sha("resume")), AnswerVariant.Resume, Text: null, Amount: null, Forwarded: null);

        var later = EngineFixture.Context("step.test.agent");

        var restarted = Assert.IsType<Applied>(Decide(parked, recipe, resume, context: later, rules: real));
        var still = Assert.IsType<Applied>(Decide(parked, recipe, resume, rules: real)).Changes[0].After;

        var after = restarted.Changes[0].After;
        Assert.Equal(("review", 2, RunToken.For(2, 1), StepRuntime.Agent), (after.State, after.Frame.Entry, after.Frame.Run?.Token, after.Frame.Run?.Runtime));
        Assert.Null(after.Suspension);
        var start = Assert.Single(restarted.FollowUps);
        Assert.Equal((FollowUpKind.StartRun, RunToken.For(2, 1), BlockId.From("step.test.agent")), (start.Kind, start.Run!.Value, start.Step!.Value));
        Assert.Equal((EngineStates.Parked, ParkReason.NoRuntime, ResumeKind.Restart), (still.State, still.Park, still.Suspension?.Resume.Kind));
        Assert.Equal(("review", 2), (still.Frame.State, still.Frame.Entry));
    }

    [Fact, Trait("Lane", "unit")]
    public void Answer_SND_WF_010_restarts_on_the_desk_in_a_build_where_the_step_became_desk_capable()
    {
        var recipe = WithAgentReview();
        var real = EngineRules.Default;
        var parked = Assert.IsType<Applied>(Decide(At(recipe, "produce"), recipe, Completed(1, "done", ("fact:output", "x")), rules: real)).Changes[0].After;
        var resume = new AnswerGiven("human:cli", new CommandStamp(CommandNonce.From(new string('c', 32)), Sha("resume")), AnswerVariant.Resume, Text: null, Amount: null, Forwarded: null);
        var deskCatalog = new BlockCatalog([.. EngineFixture.Catalog.Blocks.Select(b => b.Id.Value == "step.test.agent" ? b with { DeskCapable = true } : b)], EngineFixture.Catalog.Events);
        var later = EngineFixture.Context() with { Catalog = deskCatalog };

        var restarted = Assert.IsType<Applied>(Decide(parked, recipe, resume, context: later, rules: real));

        var after = restarted.Changes[0].After;
        Assert.Equal(("review", RunToken.For(2, 1), StepRuntime.Desk), (after.State, after.Frame.Run?.Token, after.Frame.Run?.Runtime));
        Assert.Equal(FollowUpKind.StartRun, Assert.Single(restarted.FollowUps).Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void Desk_capable_step_without_runtime_runs_on_the_desk()
    {
        var applied = Assert.IsType<Applied>(Decide(At(Step, "produce"), Step, Completed(1, "done", ("fact:output", "x"))));

        var after = applied.Changes[0].After;
        Assert.Equal(("checks", 2, StepRuntime.Desk), (after.State, after.Frame.Entry, after.Frame.Run!.Runtime));
        var start = Assert.Single(applied.FollowUps);
        Assert.Equal((FollowUpKind.StartRun, RunToken.For(2, 1), BlockId.From("step.checks")), (start.Kind, start.Run!.Value, start.Step!.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public void Applied_transition_records_one_event_with_its_produces()
    {
        var triaged = At(Item, "triaged");
        var routed = Routed("startable", outputs: ("fact:stepSpec", "Add a --version flag."));

        var applied = Assert.IsType<Applied>(Decide(triaged, Item, routed, Gates(("gate.route.startable", GateVerdict.Pass, null))));

        var (after, recorded) = (applied.Changes[0].After, applied.Changes[0].Event);
        Assert.Equal("startable", after.State);
        Assert.Equal(Fact("Add a --version flag."), after.Facts[FactKey.From("fact:stepSpec")]);
        Assert.Equal((WorkflowEventType.Transition, triaged.Id), (recorded.Type, recorded.Instance));
        Assert.Equal(Fact("Add a --version flag."), recorded.Delta.Facts?[FactKey.From("fact:stepSpec")]);
        Assert.Equal(("wait.satisfied", "startable"), (recorded.Trigger.Kind, recorded.Trigger.Outcome?.Value));
        Assert.Equal(GateOutcome.Passed, Assert.Single(recorded.Gates).Outcome);
    }

    [Fact, Trait("Lane", "unit")]
    public void Effects_are_named_by_their_identity_in_the_entered_frame()
    {
        var startable = At(Item, "startable", entry: 2);
        var budget = Gates(("gate.budget.available", GateVerdict.Pass, null));

        var first = Assert.IsType<Applied>(Decide(startable, Item, Slot(3), budget));
        var other = Assert.IsType<Applied>(Decide(startable, Item, Slot(4), budget));

        // §5.7: the identity is '{instance}:{entry}:{index}', so another grant into the same entry runs the same effect.
        var effect = Assert.Single(first.Effects);
        Assert.Equal((BlockId.From("effect.claim.ref"), CommandOrigin.Recipe, "WFI-0123456789abcdef0123456789abcdef:3:0"), (effect.Effect, effect.Origin, effect.Identity.Text));
        Assert.Equal(effect.Identity, Assert.Single(other.Effects).Identity);
        Assert.Equal([effect.Identity], first.Changes[0].Event.Effects);
    }

    [Fact, Trait("Lane", "unit")]
    public void Entering_a_child_wait_creates_the_child_in_the_same_decision()
    {
        var claimed = At(Item, "claimed", entry: 3);
        var created = new WaitSatisfied("service:workflow.inbox", new EventWaitSource(EventKind.From("claim.created"), 52), OutcomeName.From("created"), new Dictionary<FactKey, FactValue>());

        var applied = Assert.IsType<Applied>(Decide(claimed, Item, created));

        Assert.Equal(2, applied.Changes.Count);
        Assert.Equal(("running", ChildId), (applied.Changes[0].After.State, applied.Changes[0].After.Frame.Child));
        var child = applied.Changes[1];
        Assert.Equal((WorkflowEventType.Started, ChildId, "produce", 1), (child.Event.Type, child.After.Id, child.After.State, child.After.Frame.Entry));
        Assert.Contains(applied.FollowUps, f => f.Kind == FollowUpKind.StartRun && f.Instance == ChildId);
    }

    [Fact, Trait("Lane", "unit")]
    public void Human_trigger_on_revoked_recipe_gets_005_and_records_nothing()
    {
        var revoked = EngineFixture.Context() with { Approval = ApprovalCheck.Revoked };

        var decision = Decide(At(Step, "produce"), Step, Completed(1, "done", ("fact:output", "x")), context: revoked);

        Assert.Equal("SND-RECIPE-005", Assert.IsType<Rejected>(decision).Error.Code);
    }

    [Fact, Trait("Lane", "unit")]
    public void System_trigger_on_drifted_recipe_parks_spec_conflict()
    {
        var drifted = EngineFixture.Context() with { Approval = ApprovalCheck.Drifted };

        var applied = Assert.IsType<Applied>(Decide(At(Item, "triaged"), Item, Routed("startable"), context: drifted));

        var top = applied.Changes[0].After.Suspension!; // why: a drift park, asserted next
        Assert.Equal((ParkReason.SpecConflict, ParkKind.Drift, ResumeKind.Rearm), (top.Reason, top.ParkKind, top.Resume.Kind));
    }

    private static WaitSatisfied Slot(long grant) =>
        new("service:workflow.inbox", new ResourceWaitSource(ResourceKind.Slot, grant), Outcome: null, new Dictionary<FactKey, FactValue>());

    private static Recipe WithAgentReview()
    {
        var recipe = Minimal([From("review", ARecipeWhen("done")), new(["review"], "done", "step.completed", ARecipeWhen("done"), [], [], [], [])]);
        var states = new Dictionary<string, RecipeState>(recipe.States, StringComparer.Ordinal)
        {
            ["review"] = recipe.States["produce"] with { Step = new StepUse(BlockId.From("step.test.agent"), new Dictionary<string, WithValue>(StringComparer.Ordinal), []) },
        };
        return recipe with { States = states };
    }

    private static WhenClause ARecipeWhen(string outcome) => ARecipe.When(OutcomeName.From(outcome));

    private static Recipe ChildParent => RecipeComposition.ReadClean(ShippedRecipes.Read("dev/workflow.dev-child-outputs.yaml"), "workflow.dev-child-outputs.yaml").Recipe;

    /// <summary>The dev parent waiting in 'run', its child finished as resolved with fact:output.</summary>
    private static (WorkflowInstance Waiting, WaitSatisfied Ended) ChildEnded()
    {
        var run = At(ChildParent, "run");
        var waiting = run with { Frame = run.Frame with { Child = ChildId } };
        var ended = new WaitSatisfied(
            "service:workflow.inbox", new ChildWaitSource(ChildId, TriggerKey.For(ChildId, "end")), OutcomeName.From("resolved"),
            new Dictionary<FactKey, FactValue> { [FactKey.From("fact:output")] = Fact("summary-week-41.csv") });
        return (waiting, ended);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_renamed_child_output_is_stored_under_its_new_key()
    {
        var (waiting, ended) = ChildEnded();

        var applied = Assert.IsType<Applied>(Decide(waiting, ChildParent, ended));

        var after = applied.Changes[0].After;
        Assert.Equal("review", after.State);
        Assert.Equal(Fact("summary-week-41.csv"), after.Facts[FactKey.From("fact:exported")]);
        Assert.Equal(Fact("summary-week-41.csv"), applied.Changes[0].Event.Delta.Facts?[FactKey.From("fact:exported")]);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bare_child_output_is_stored_under_its_key()
    {
        var (waiting, ended) = ChildEnded();

        var applied = Assert.IsType<Applied>(Decide(waiting, ChildParent, ended));

        Assert.Equal(Fact("summary-week-41.csv"), applied.Changes[0].After.Facts[FactKey.From("fact:output")]);
    }
}
