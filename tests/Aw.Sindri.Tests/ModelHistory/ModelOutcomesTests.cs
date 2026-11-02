using Aw.Kernel.Testing;
using Aw.Sindri.Core.ModelHistory;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ModelHistory;

/// <summary>What a failed Step run counts as for the model that ran it (the recorded failure kind first, the reason rule when the row has none).</summary>
[Lane(TestLane.Unit)]
public sealed class ModelOutcomesTests
{
    [Theory]
    [InlineData("invalid-result: not json", ModelOutcome.Refused)]
    [InlineData("invalid-result:", ModelOutcome.Refused)]
    [InlineData("exit", ModelOutcome.Crashed)]
    [InlineData("lost", ModelOutcome.Crashed)]
    [InlineData("idle", ModelOutcome.Crashed)]
    [InlineData("launch", ModelOutcome.Crashed)]
    [InlineData("tests-failed", ModelOutcome.CheckFailed)]
    [InlineData("exit code 2", ModelOutcome.CheckFailed)]
    [InlineData("Exit", ModelOutcome.CheckFailed)]
    [InlineData("an invalid-result: later in the text", ModelOutcome.CheckFailed)]
    [InlineData("", ModelOutcome.CheckFailed)]
    [InlineData(null, ModelOutcome.CheckFailed)]
    public void A_reason_reads_as_the_outcome_of_its_kind(string? reason, ModelOutcome expected) =>
        Expect.Equal(expected, ModelOutcomes.OfFailure(failureKind: null, reason));

    [Theory]
    [InlineData("refused", "tests-failed", ModelOutcome.Refused)]
    [InlineData("check_failed", "invalid-result: x", ModelOutcome.CheckFailed)]
    [InlineData("crashed", "tests-failed", ModelOutcome.Crashed)]
    [InlineData("a-kind-from-the-future", "exit", ModelOutcome.Crashed)]
    [InlineData("", "invalid-result: x", ModelOutcome.Refused)]
    public void A_recorded_failure_kind_wins_over_the_reason_and_an_unknown_one_leaves_it_to_the_reason(string failureKind, string reason, ModelOutcome expected) =>
        Expect.Equal(expected, ModelOutcomes.OfFailure(failureKind, reason));
}
