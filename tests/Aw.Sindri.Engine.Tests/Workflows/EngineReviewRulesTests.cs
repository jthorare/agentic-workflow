using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The pure rules the engine P5 review tightened: who may send an 'open-request', and what a handback that kept nothing asks of the timers.</summary>
public sealed class EngineReviewRulesTests
{
    private static readonly TimeSpan _floor = TimeSpan.FromDays(2);

    [Fact, Trait("Lane", "unit")]
    public void An_open_request_from_any_actor_but_the_engine_is_SND_WF_003()
    {
        var produce = At(Step, "produce");

        var rejected = Assert.IsType<Rejected>(Real(produce, new OpenRequested("human:cli", "LCH-0123456789abcdef0123456789abcdef")));
        var accepted = Assert.IsType<Applied>(Real(produce, new OpenRequested(EngineActor.Name, "LCH-0123456789abcdef0123456789abcdef")));

        Assert.Equal("SND-WF-003", rejected.Error.Code);
        Assert.Equal(EngineActor.Name, accepted.Changes[0].After.Suspension?.Actor);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("")]
    [InlineData("LCH-0123")]
    [InlineData("lch-0123456789abcdef0123456789abcdef")]
    [InlineData("LCH-0123456789ABCDEF0123456789abcdef")]
    public void An_open_request_whose_launch_is_not_a_launch_id_is_SND_WF_003(string launch)
    {
        var rejected = Assert.IsType<Rejected>(Real(At(Step, "produce"), new OpenRequested(EngineActor.Name, launch)));

        Assert.Equal("SND-WF-003", rejected.Error.Code);
        Assert.Contains("LCH-", rejected.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_handback_whose_takeover_kept_nothing_resumes_the_full_timeout_so_takeoverIdle_stops()
    {
        var produce = At(Step, "produce");
        var taken = After(Real(produce, new TakeoverRequested("human:cli", Stamp('a'))));
        var back = After(Real(taken, new HandbackRequested("human:cli", Stamp('b'))));

        var action = StateTimers.Between(taken, back, Step, _floor);

        var full = Assert.NotNull(StateTimers.Timeout(Step.States["produce"], back.Frame.Run, _floor));
        Assert.Equal(new TimerAction(TimerActionKind.Resume, DeadlineKind.State, full), action);
    }

    private static Decision Real(WorkflowInstance instance, Trigger trigger) => Decide(instance, Step, trigger, rules: EngineRules.Default);

    private static WorkflowInstance After(Decision decision) => Assert.IsType<Applied>(decision).Changes[0].After;

    private static CommandStamp Stamp(char digit) => new(CommandNonce.From(new string(digit, 32)), new string('0', 64));
}
