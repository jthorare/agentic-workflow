using Aw.Kernel.Testing;
using Aw.Sindri.Core.BugFix;
using Aw.Sindri.Features.BugFix;

namespace Aw.Sindri.Tests.Workflow.BugFix;

/// <summary>'step.bugfix.start-attempt': the mode the route chose opens bugfix-state's next attempt; the cap is bugfix-state's, reported as 'capReached'.</summary>
[Lane(TestLane.Unit)]
public sealed class StartAttemptStepTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("single", AttemptMode.Whole)]
    [InlineData("split", AttemptMode.Split)]
    public async Task The_chosen_mode_opens_attempt_one_and_is_reported_with_it(string choice, AttemptMode mode)
    {
        var rig = new BugFixRig();

        var result = await new StartAttemptStep(rig.State, rig.Area).Run(BugFixRig.Context("step.bugfix.start-attempt", ("decision", BugFixRig.Decision(choice))), Token);

        Assert.Equal(choice, result.Result!.Outcome.Value);
        Assert.Equal(1, (int?)BugFixRig.Out(result, "attempt")!["n"]);
        Assert.Equal(choice, (string?)BugFixRig.Out(result, "attempt")!["mode"]);
        Assert.Equal(["start-attempt " + mode], rig.State.Calls);
    }

    [Fact]
    public async Task The_sixth_attempt_is_capReached_and_writes_nothing()
    {
        var rig = new BugFixRig();
        var step = new StartAttemptStep(rig.State, rig.Area);
        for (var round = 1; round <= 5; round++)
        {
            var number = round.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Assert.Equal("single", (await step.Run(BugFixRig.Context("step.bugfix.start-attempt", ("decision", BugFixRig.Decision("single"))), Token)).Result!.Outcome.Value);
            _ = await rig.State.RecordCandidate(rig.StateDirectory, new CandidateSource("b" + number, "/w" + number), Token);
            _ = await rig.State.Recheck(rig.StateDirectory, "c" + number, "/w" + number, new FrozenCheck("test", "t", ["t"], "."), Token);
        }

        var sixth = await step.Run(BugFixRig.Context("step.bugfix.start-attempt", ("decision", BugFixRig.Decision("single"))), Token);

        Assert.Equal("capReached", sixth.Result!.Outcome.Value);
        Assert.Empty(sixth.Result.Outputs);
    }

    [Theory]
    [InlineData("""{"choice":"sideways","provider":"desk"}""")]
    [InlineData("not json")]
    public async Task A_decision_that_names_neither_mode_fails_the_Step(string decision)
    {
        var rig = new BugFixRig();

        var result = await new StartAttemptStep(rig.State, rig.Area).Run(BugFixRig.Context("step.bugfix.start-attempt", ("decision", decision)), Token);

        Assert.Null(result.Result);
        Assert.Contains("single", result.Failure, StringComparison.Ordinal);
        Assert.Empty(rig.State.Calls);
    }

    [Fact]
    public async Task A_refusal_that_is_not_the_cap_fails_the_Step_with_bugfix_states_reason()
    {
        var rig = new BugFixRig();
        var step = new StartAttemptStep(rig.State, rig.Area);
        _ = await step.Run(BugFixRig.Context("step.bugfix.start-attempt", ("decision", BugFixRig.Decision("single"))), Token);
        _ = await rig.State.RecordCandidate(rig.StateDirectory, new CandidateSource("b1", "/w1"), Token);

        var second = await step.Run(BugFixRig.Context("step.bugfix.start-attempt", ("decision", BugFixRig.Decision("single"))), Token);

        Assert.Null(second.Result);
        Assert.Contains("AW-BUGFIX-005", second.Failure, StringComparison.Ordinal);
        Assert.Contains("candidate c1 has no run yet", second.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_bug_fix_that_was_never_started_fails_the_Step()
    {
        var result = await new StartAttemptStep(new Testing.BugFix.InMemoryBugFixState(), new Testing.Scoping.InMemoryRunArea()).Run(BugFixRig.Context("step.bugfix.start-attempt", ("decision", BugFixRig.Decision("single"))), Token);

        Assert.Null(result.Result);
        Assert.Contains("AW-BUGFIX-002", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_context_with_no_address_fails_the_Step()
    {
        var rig = new BugFixRig();

        var result = await new StartAttemptStep(rig.State, rig.Area).Run(BugFixRig.Context("step.bugfix.start-attempt", ("decision", BugFixRig.Decision("single"))) with { Address = null }, Token);

        Assert.Null(result.Result);
        Assert.Contains("address", result.Failure, StringComparison.Ordinal);
    }
}
