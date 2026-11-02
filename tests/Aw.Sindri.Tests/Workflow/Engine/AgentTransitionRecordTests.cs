using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using static Aw.Sindri.Tests.Workflow.Engine.LoopFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>An agent Step's transition records the launcher agent and model it ran on, not only launch.started (#1269 ask 3); any other Step's records neither.</summary>
public sealed class AgentTransitionRecordTests
{
    [Fact, Trait("Lane", "unit")]
    public void An_agent_report_carries_its_agent_and_model_onto_the_transition_row()
    {
        var report = Report() with { Via = StepRuntime.Agent, Agent = "codex", Model = "gpt-5" };
        var change = Decide(report);

        Assert.Equal(("codex", "gpt-5"), (change.Event.Trigger.Agent, change.Event.Trigger.Model));
        var detail = WorkflowEventDetail.Of(change.Event, report);
        Assert.Equal(("codex", "gpt-5"), (detail.Agent, detail.Model));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_agent_report_that_named_no_model_records_the_agent_alone()
    {
        var change = Decide(Report() with { Via = StepRuntime.Agent, Agent = "claude-code" });

        Assert.Equal(("claude-code", null), (change.Event.Trigger.Agent, change.Event.Trigger.Model));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_block_report_records_neither()
    {
        var change = Decide(Report() with { Via = StepRuntime.Block, Agent = "codex", Model = "gpt-5" });

        Assert.Equal((null, null), (change.Event.Trigger.Agent, change.Event.Trigger.Model));
    }

    private static InstanceChange Decide(StepCompleted report)
    {
        var instance = In("produce", new LoopState(1, 3, Last: null), Spent(0, 0));
        return Assert.IsType<Applied>(TransitionDecision.Decide(new DecisionInput(instance, StepRecipe, report, GateAnswers.None, EngineFixture.Context()))).Changes[0];
    }

    private static StepCompleted Report() => EngineFixture.Completed(3, "done", ("fact:output", "the draft"));
}
