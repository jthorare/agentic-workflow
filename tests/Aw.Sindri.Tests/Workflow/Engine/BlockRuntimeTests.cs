using Aw.Kernel.Testing;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Tests.GetInstance;
using Aw.Sindri.Tests.Workflow.Choice;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>The pure side of the block runtime (DT spec §8.2): which runtime a run opens on, what a block run shows and arms, and how a block result becomes a trigger.</summary>
[Lane(TestLane.Unit)]
public sealed class BlockRuntimeTests
{
    private static readonly BlockId _judge = BlockId.From("step.judge.choose");
    private static readonly Recipe _route = EvalComposition.Recipe(RouteRecipes.Dev);

    [Fact]
    public void A_block_runtime_step_without_a_registered_runtime_opens_a_desk_run_and_stays_human_only()
    {
        var entered = StateEntry.Enter(EngineFixture.At(_route, "evidence-ui"), _route, "route-evidence", EngineFixture.Context());

        Assert.Equal(StepRuntime.Desk, entered.Frame.Run?.Runtime);
        Assert.True(HumanOnlySteps.Applies(EngineFixture.Catalog.Find(_judge)!, StepRuntime.Desk));
        Assert.False(HumanOnlySteps.Applies(EngineFixture.Catalog.Find(_judge)!, StepRuntime.Block));
    }

    [Fact]
    public void A_registered_Step_whose_descriptor_is_not_a_block_one_opens_a_desk_run_like_an_unregistered_one()
    {
        var deskTwin = BlockId.From("step.desk.choose");
        var context = EngineFixture.Context() with { BlockRuntimes = new HashSet<BlockId> { deskTwin } };

        var entered = StateEntry.Enter(EngineFixture.At(_route, "route-evidence-desk"), _route, "route-evidence-desk", context);

        Assert.Equal(StepRuntime.Desk, entered.Frame.Run?.Runtime);
    }

    [Fact]
    public void A_block_run_has_no_desk_task_or_desk_floor()
    {
        var instance = BlockRun();
        var floor = TimeSpan.FromDays(10);

        var view = InstanceViews.Of(instance, Interpreted(), EngineFixture.Now, timeoutAt: null, new InstanceViewContext());

        Assert.Null(view.Task);
        Assert.Equal(1, view.RunningSteps);
        Assert.Equal(TimeSpan.FromDays(2), StateTimers.Timeout(_route.States["route-evidence"], instance.Frame.Run, floor));
        Assert.Equal(floor, StateTimers.Timeout(_route.States["route-evidence"], instance.Frame.Run! with { Runtime = StepRuntime.Desk }, floor));
    }

    [Fact]
    public void A_block_result_is_validated_like_a_desk_report()
    {
        var instance = BlockRun();
        var trigger = BlockStepTriggers.Completed(instance, _route, Context(), new BlockCompletion(_judge, instance.Frame.Run!.Token, OutcomeName.From("ui"), Decision()));

        var completed = Assert.IsType<StepCompleted>(trigger);
        Assert.Equal(("service:block:step.judge.choose", StepRuntime.Block), (completed.Actor, completed.Via));
        var decision = TransitionDecision.Decide(new DecisionInput(instance, _route, trigger, GateAnswers.None, Context()));
        Assert.Equal("evidence-ui", Assert.IsType<Applied>(decision).Changes[0].After.State);
    }

    [Fact]
    public void A_block_result_s_event_says_via_block_and_names_the_decision()
    {
        var instance = BlockRun();
        var trigger = BlockStepTriggers.Completed(instance, _route, Context(), new BlockCompletion(_judge, instance.Frame.Run!.Token, OutcomeName.From("ui"), Decision(), "JD-7"));

        var recorded = Assert.IsType<Applied>(TransitionDecision.Decide(new DecisionInput(instance, _route, trigger, GateAnswers.None, Context()))).Changes[0].Event;
        var detail = WorkflowEventDetail.Of(recorded, trigger);

        Assert.Equal(("block", "JD-7"), (recorded.Trigger.Via, recorded.Trigger.Decision));
        Assert.Equal(("block", "JD-7"), (detail.Via, detail.Decision));
    }

    [Theory]
    [InlineData("human:cli", StepRuntime.Desk)]
    [InlineData("agent:x", StepRuntime.Agent)]
    public void A_report_from_another_runtime_on_a_block_run_is_refused_not_allowed(string actor, StepRuntime via)
    {
        var instance = BlockRun();
        var report = new StepCompleted(actor, instance.Frame.Run!.Token, OutcomeName.From("ui"), Decision(), Tokens: 0, via, new string('f', 64));

        var decision = TransitionDecision.Decide(new DecisionInput(instance, _route, report, GateAnswers.None, Context()));

        var rejected = Assert.IsType<Rejected>(decision);
        Assert.Equal(WorkflowErrors.TriggerNotAllowed.Code, rejected.Error.Code);
        Assert.Contains("take it over", rejected.Error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("bogus", true, "invalid-result: SND-WF-008")]
    [InlineData("ui", false, "invalid-result: SND-WF-008")]
    public void An_invalid_block_result_becomes_step_failed(string outcome, bool withDecision, string reason)
    {
        var instance = BlockRun();
        var outputs = withDecision ? Decision() : [];

        var trigger = BlockStepTriggers.Completed(instance, _route, Context(), new BlockCompletion(_judge, instance.Frame.Run!.Token, OutcomeName.From(outcome), outputs));

        var failed = Assert.IsType<StepFailed>(trigger);
        Assert.StartsWith(reason, failed.Reason, StringComparison.Ordinal);
        Assert.Equal("service:block:step.judge.choose", failed.Actor);
    }

    [Fact]
    public void A_failed_block_run_is_step_failed_and_takes_the_routes_failed_edge_to_the_desk_twin()
    {
        var instance = BlockRun();
        var trigger = BlockStepTriggers.Failed(_judge, instance.Frame.Run!.Token, "timeout");

        var decision = TransitionDecision.Decide(new DecisionInput(instance, _route, trigger, GateAnswers.None, Context()));

        Assert.Equal("route-evidence-desk", Assert.IsType<Applied>(decision).Changes[0].After.State);
    }

    private static DecisionContext Context() => EngineFixture.Context() with { BlockRuntimes = new HashSet<BlockId> { _judge } };

    private static WorkflowInstance BlockRun()
    {
        var entered = StateEntry.Enter(EngineFixture.At(_route, "evidence-ui"), _route, "route-evidence", Context());
        Assert.Equal(StepRuntime.Block, entered.Frame.Run?.Runtime);
        return entered;
    }

    private static InterpretedRecipe Interpreted() => StatusCases.Recipe with { Recipe = _route, Catalog = EngineFixture.Catalog };

    private static Dictionary<FactKey, FactValue> Decision() => new() { [FactKey.From("fact:decision")] = EngineFixture.Fact("{\"choice\":\"ui\"}") };
}
