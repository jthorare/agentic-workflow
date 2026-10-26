using System.Globalization;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// A Step failure no transition takes is a dependency that is down, not a question for a human: the engine runs the Step again twice, then parks
/// it as 'unavailable' (owner decision q90, q93), which asks no one and resumes by a rerun. The 'step-unavailable-v1' patch keeps a history from
/// before it parking with needs-answer at once.
/// </summary>
public sealed class UnavailableParkTests
{
    private const string ProduceStep = "step.produce";

    [Fact, Trait("Lane", "unit")]
    public void The_first_failure_runs_the_ProduceStepagain_and_parks_nothing()
    {
        var applied = Assert.IsType<Applied>(Decide(At(Step, "produce"), Step, Failed(1, 1, "the launcher was cut off")));

        var after = applied.Changes[0].After;
        Assert.Null(after.Suspension);
        Assert.Equal((RunToken.For(1, 2), 1), (after.Frame.Run!.Token, after.Frame.Run.Retries));
        var start = Assert.Single(applied.FollowUps);
        Assert.Equal((FollowUpKind.StartRun, RunToken.For(1, 2)), (start.Kind, start.Run!.Value));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_second_failure_runs_it_a_second_time()
    {
        var second = Retried(At(Step, "produce"), failures: 1);

        var applied = Assert.IsType<Applied>(Decide(second, Step, Failed(1, 2, "the launcher was cut off")));

        var after = applied.Changes[0].After;
        Assert.Null(after.Suspension);
        Assert.Equal((RunToken.For(1, 3), 2), (after.Frame.Run!.Token, after.Frame.Run.Retries));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_third_failure_parks_unavailable_with_the_dependency_the_retries_and_the_last_error()
    {
        var third = Retried(At(Step, "produce"), failures: 2);

        var applied = Assert.IsType<Applied>(Decide(third, Step, Failed(1, 3, "exit") with { Dependency = "the agent launcher", Code = "SND-LAUNCH-007" }));

        var after = applied.Changes[0].After;
        Assert.Equal((EngineStates.Parked, ParkReason.Unavailable), (after.State, after.Park));
        var park = after.Suspension!;
        Assert.Equal((ParkKind.Step, ResumeKind.Rerun, "SND-LAUNCH-007"), (park.ParkKind, park.Resume.Kind, ParkCodes.Of(park)));
        Assert.Equal("the agent launcher is unavailable after 2 retries; the last error: exit", park.Question);
        Assert.Empty(applied.FollowUps);
    }

    [Fact, Trait("Lane", "unit")]
    public void Without_a_named_dependency_the_park_names_the_ProduceStepand_carries_the_launcher_words_after_the_reason()
    {
        var third = Retried(At(Step, "produce"), failures: 2);

        var applied = Assert.IsType<Applied>(Decide(third, Step, Failed(1, 3, "launch") with { Detail = "SND-LAUNCH-007: worktree add was cut off" }));

        var park = applied.Changes[0].After.Suspension!;
        Assert.Equal($"{ProduceStep} is unavailable after 2 retries; the last error: launch: SND-LAUNCH-007: worktree add was cut off", park.Question);
        Assert.Null(ParkCodes.Of(park));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("exit", null, "exit")]
    [InlineData("lost", "the session vanished", "lost: the session vanished")]
    public void A_gone_agent_session_is_relaunched_twice_then_parks_as_gone(string reason, string? detail, string lastError)
    {
        var first = Assert.IsType<Applied>(Decide(At(Step, "produce"), Step, Failed(1, 1, reason) with { Detail = detail }));
        var afterFirst = first.Changes[0].After;
        Assert.Null(afterFirst.Suspension);
        Assert.Equal((RunToken.For(1, 2), 1), (afterFirst.Frame.Run!.Token, afterFirst.Frame.Run.Retries));
        Assert.Equal((FollowUpKind.StartRun, RunToken.For(1, 2)), (Assert.Single(first.FollowUps).Kind, first.FollowUps[0].Run!.Value));

        var second = Assert.IsType<Applied>(Decide(afterFirst, Step, Failed(1, 2, reason) with { Detail = detail }));
        var afterSecond = second.Changes[0].After;
        Assert.Null(afterSecond.Suspension);
        Assert.Equal((RunToken.For(1, 3), 2), (afterSecond.Frame.Run!.Token, afterSecond.Frame.Run.Retries));
        Assert.Equal(FollowUpKind.StartRun, Assert.Single(second.FollowUps).Kind);

        var third = Assert.IsType<Applied>(Decide(afterSecond, Step, Failed(1, 3, reason) with { Detail = detail }));
        var after = third.Changes[0].After;
        Assert.Equal((EngineStates.Parked, ParkReason.Unavailable), (after.State, after.Park));
        Assert.Equal($"{ProduceStep}'s agent session is gone ({reason}), relaunched 2 times; the last error: {lastError}", after.Suspension!.Question);
        Assert.Empty(third.FollowUps);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("service:agent:" + ProduceStep, "idle")]
    [InlineData("service:agent:" + ProduceStep, "launch")]
    [InlineData("service:block:" + ProduceStep, "exit")]
    public void Any_other_failure_keeps_the_unavailable_text(string actor, string reason)
    {
        var third = Retried(At(Step, "produce"), failures: 2);

        var applied = Assert.IsType<Applied>(Decide(third, Step, Failed(1, 3, reason) with { Actor = actor }));

        Assert.Equal($"{ProduceStep} is unavailable after 2 retries; the last error: {reason}", applied.Changes[0].After.Suspension!.Question);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_rerun_answer_starts_the_count_again()
    {
        var parked = Assert.IsType<Applied>(Decide(Retried(At(Step, "produce"), failures: 2), Step, Failed(1, 3, "exit"))).Changes[0].After;

        var applied = Assert.IsType<Applied>(Decide(parked, Step, Resume()));

        var after = applied.Changes[0].After;
        Assert.Null(after.Suspension);
        Assert.Equal((RunToken.For(1, 4), 0), (after.Frame.Run!.Token, after.Frame.Run.Retries));
        Assert.Equal(FollowUpKind.StartRun, Assert.Single(applied.FollowUps).Kind);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_history_from_before_the_patch_parks_needs_answer_at_once()
    {
        var legacy = EngineFixture.Context() with { LegacyUnavailable = true };

        var applied = Assert.IsType<Applied>(Decide(At(Step, "produce"), Step, Failed(1, 1, "The build broke."), context: legacy));

        var after = applied.Changes[0].After;
        Assert.Equal((EngineStates.Parked, ParkReason.NeedsAnswer), (after.State, after.Park));
        Assert.Equal((ParkKind.Step, ResumeKind.Rerun), (after.Suspension?.ParkKind, after.Suspension?.Resume.Kind));
        Assert.Empty(applied.FollowUps);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unavailable_park_offers_a_resume_and_no_text()
    {
        var parked = Assert.IsType<Applied>(Decide(Retried(At(Step, "produce"), failures: 2), Step, Failed(1, 3, "exit"))).Changes[0].After;

        Assert.Equal([AnswerVariant.Resume], AnswerVariants.Fitting(parked.Suspension!));
    }

    private static WorkflowInstance Retried(WorkflowInstance instance, int failures)
    {
        var run = instance.Frame.Run!;
        return instance with { Frame = instance.Frame with { Run = run with { Token = RunToken.For(instance.Frame.Entry, failures + 1), Retries = failures } } };
    }

    private static StepFailed Failed(int entry, int run, string reason) =>
        new("service:agent:" + ProduceStep, RunToken.For(entry, run), reason, Tokens: 0, Sha(string.Create(CultureInfo.InvariantCulture, $"{reason}\n{entry}.{run}")));

    private static AnswerGiven Resume() => new(
        "human:cli", new CommandStamp(CommandNonce.From(new string('c', 32)), new string('d', 64)), AnswerVariant.Resume, Text: null, Amount: null, Forwarded: null);

    private static Decision Decide(WorkflowInstance instance, Recipe recipe, Trigger trigger, DecisionContext? context = null) =>
        EngineFixture.Decide(instance, recipe, trigger, context: context);
}
