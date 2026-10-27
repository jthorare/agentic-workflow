using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// A rewind refuses a run whose agent is live (#1270), not one whose launch is merely still on the run: a park that kept its launch after the agent's session
/// ended (failed, idle, lost) rewinds. The command service reads the ledger's session state and passes the ended launch in.
/// </summary>
public sealed class RewindEndedAgentTests
{
    private static readonly string _launch = "LCH-" + new string('a', 32);

    private static readonly string[] _facts = ["fact:questions", "fact:answerLog"];

    private static readonly string[] _walked = ["intake", "read-brief", "gather", "draft", "check", "questions", "interview", "record", "apply"];

    private static Core.Workflow.Recipes.Recipe Scoping => RecipeComposition.ReadClean(Testing.Workflow.ShippedRecipes.Read("sindri/workflow.scoping.yaml"), "workflow.scoping.yaml").Recipe;

    private static WorkflowInstance WithLaunch()
    {
        var instance = At(Scoping, "draft", entry: 5) with
        {
            Visited = _walked,
            Facts = _facts.ToDictionary(static n => FactKey.From(n), static n => Fact(n)),
        };
        return instance with { Frame = instance.Frame with { Run = new OpenRun(RunToken.For(5, 1), StepRuntime.Agent, _launch, Task: null) } };
    }

    private static Decision Decide(RewindRequested rewind) =>
        TransitionDecision.Decide(new DecisionInput(WithLaunch(), Scoping, rewind, GateAnswers.None, EngineFixture.Context()));

    private static CommandStamp Stamp() => new(CommandNonce.From(new string('e', 32)), new string('0', 64));

    [Fact, Trait("Lane", "unit")]
    public void A_run_whose_launch_session_already_ended_rewinds()
    {
        var applied = Assert.IsType<Applied>(Decide(new RewindRequested("human:cli", Stamp(), "interview", EndedLaunch: _launch)));

        var after = applied.Changes[0].After;
        Assert.Equal(("interview", 6), (after.Frame.State, after.Frame.Entry));
        Assert.Null(after.Frame.Run?.Launch);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_ended_launch_that_is_not_the_runs_launch_does_not_open_the_rewind()
    {
        var rejected = Assert.IsType<Rejected>(Decide(new RewindRequested("human:cli", Stamp(), "interview", EndedLaunch: "LCH-" + new string('b', 32))));

        Assert.Contains(_launch, rejected.Error.Message, StringComparison.Ordinal);
    }
}
