using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// A refused agent launch in the decision (sandbox spec SB16, N1; engine P8 U9): no runtime closes the run and either opens the desk twin in the same frame
/// (no round, no Step failure) or parks 'SND-WF-010' with the sandbox marker; an unconfinable Step or a failed self-check parks 'launch'; either park's Resume starts a new run.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class LaunchRefusalDecisionTests
{
    private static readonly RunToken _run = RunToken.For(1, 1);

    private static WorkflowInstance Running()
    {
        var at = EngineFixture.At(EngineFixture.Step, "produce");
        var loops = new Dictionary<string, LoopState>(StringComparer.Ordinal) { ["revisions"] = new LoopState(3, 3, Last: null) };   // the last round of the cap
        return at with { Frame = at.Frame with { Run = new OpenRun(_run, StepRuntime.Agent, "LCH-" + new string('a', 32), Task: null) }, Loops = loops };
    }

    private static DecisionContext Deskable() => EngineFixture.Context("step.produce");

    private static DecisionContext AgentOnly()
    {
        var blocks = EngineFixture.Catalog.Blocks.Select(static b => b.Id.Value == "step.produce" ? b with { DeskCapable = false } : b);
        return Deskable() with { Catalog = new BlockCatalog([.. blocks], EngineFixture.Catalog.Events) };
    }

    private static RuntimeUnavailable Unavailable(RunToken? run = null) =>
        new("service:agent:step.produce", run ?? _run, "SND-LAUNCH-014", "no-bwrap", Source: null, "no sandbox tool");

    private static LaunchRefused Refused(string code = "SND-LAUNCH-015", string reason = "form-none") =>
        new("service:agent:step.produce", _run, code, reason, Source: null, "cannot confine");

    private static Decision Decide(WorkflowInstance instance, Trigger trigger, DecisionContext context) =>
        EngineFixture.Decide(instance, EngineFixture.Step, trigger, context: context, rules: EngineRules.Default);

    private static AnswerGiven Resume() =>
        new("human:cli", new CommandStamp(CommandNonce.From(new string('c', 32)), EngineFixture.Sha("resume")), AnswerVariant.Resume, Text: null, Amount: null, Forwarded: null);

    private static WorkflowInstance Parked(Trigger trigger, DecisionContext context) =>
        Assert.IsType<Applied>(Decide(Running(), trigger, context)).Changes[0].After;

    [Fact]
    public void A_desk_capable_step_takes_the_desk_in_the_same_frame_and_counts_no_round()
    {
        var before = Running();

        var applied = Assert.IsType<Applied>(Decide(before, Unavailable(), Deskable()));

        var after = applied.Changes[0].After;
        Assert.Equal(("produce", 1, RunToken.For(1, 2), StepRuntime.Desk), (after.Frame.State, after.Frame.Entry, after.Frame.Run?.Token, after.Frame.Run?.Runtime));
        Assert.NotNull(after.Frame.Run?.Task);
        Assert.Null(after.Suspension);
        Assert.Equal(before.Loops["revisions"].Round, after.Loops["revisions"].Round);
        Assert.Equal(before.Spent, after.Spent);
        Assert.Equal(TriggerNames.RuntimeUnavailable, applied.Changes[0].Event.Trigger.Kind);
        Assert.Equal(RunToken.For(1, 2), Assert.Single(applied.FollowUps).Run);
    }

    [Fact]
    public void A_step_that_cannot_run_at_the_desk_parks_no_runtime_with_the_sandbox_marker()
    {
        var after = Parked(Unavailable(), AgentOnly());

        Assert.Equal((EngineStates.Parked, ParkReason.NoRuntime, ResumeKind.Rerun), (after.State, after.Park, after.Suspension?.Resume.Kind));
        Assert.Equal(new SandboxPark("SND-LAUNCH-014", "no-bwrap", Source: null), after.Suspension?.Sandbox);
    }

    [Fact]
    public void Resuming_a_no_runtime_park_opens_a_new_agent_run_and_counts_no_round()
    {
        var parked = Parked(Unavailable(), AgentOnly());

        var resumed = Assert.IsType<Applied>(Decide(parked, Resume(), AgentOnly()));

        var after = resumed.Changes[0].After;
        Assert.Equal((RunToken.For(1, 2), StepRuntime.Agent, null), (after.Frame.Run?.Token, after.Frame.Run?.Runtime, after.Frame.Run?.Launch));
        Assert.Null(after.Suspension);
        Assert.Equal(3, after.Loops["revisions"].Round);   // the cap was not asked: the launch never ran a round
        Assert.Equal(RunToken.For(1, 2), Assert.Single(resumed.FollowUps).Run);
    }

    [Theory]
    [InlineData("SND-LAUNCH-015", "form-none", ParkKind.Unconfinable)]
    [InlineData("SND-LAUNCH-016", "git-ref", ParkKind.SelfCheck)]
    public void An_unconfinable_step_or_a_failed_self_check_parks_launch(string code, string reason, ParkKind kind)
    {
        var after = Parked(Refused(code, reason), Deskable());

        Assert.Equal((EngineStates.Parked, ParkReason.Launch, kind, ResumeKind.Rerun), (after.State, after.Park, after.Suspension?.ParkKind, after.Suspension?.Resume.Kind));
        Assert.Equal(new SandboxPark(code, reason, Source: null), after.Suspension?.Sandbox);
    }

    [Fact]
    public void The_launch_park_takes_resume_only_and_starts_a_new_run_without_a_round()
    {
        var parked = Parked(Refused(), Deskable());

        var resumed = Assert.IsType<Applied>(Decide(parked, Resume(), Deskable()));
        var text = Decide(parked, Resume() with { Variant = AnswerVariant.Text, Text = "try" }, Deskable());

        var after = resumed.Changes[0].After;
        Assert.Equal((RunToken.For(1, 2), StepRuntime.Agent, 3), (after.Frame.Run?.Token, after.Frame.Run?.Runtime, after.Loops["revisions"].Round));
        Assert.Contains("parked (launch)", Assert.IsType<Rejected>(text).Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_refusal_for_a_run_that_is_not_open_is_stale()
    {
        var rejected = Assert.IsType<Rejected>(Decide(Running(), Unavailable(RunToken.For(1, 9)), Deskable()));

        Assert.Equal("SND-WF-005", rejected.Error.Code);
    }

    [Fact]
    public void A_refusal_while_the_instance_is_parked_is_not_allowed()
    {
        var parked = Parked(Refused(), Deskable());

        _ = Assert.IsType<Rejected>(Decide(parked, Refused(), Deskable()));
    }
}
