using Aw.Judge.Testing;
using Aw.Kernel.Testing;

namespace Aw.Judge.Tests.ApproveDecision;

// The contract of 'judge approve': the decision row with approved_at set (the first approval's time); unknown → exit 1.
public sealed class ApproveDecisionCliTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task Approve_prints_the_row_with_approved_at_and_the_reason_code_unchanged()
    {
        _ = await _state.DecisionLog().Record(A.Decision().WithId("d-1").Build(), CancellationToken.None);
        var cli = new JudgeCli(_state);
        cli.Clock.Advance(TimeSpan.FromMinutes(5));

        var run = await cli.Run("approve", "d-1");

        Assert.Equal((0, ""), (run.ExitCode, run.Stderr));
        DecisionJson.AssertRow(run.Json(), "d-1", "model", undone: false);
        Assert.Equal(KnownInstants.Epoch.AddMinutes(5), run.Json().GetProperty("approved_at").GetDateTimeOffset());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Approving_twice_keeps_the_first_approved_at()
    {
        _ = await _state.DecisionLog().Record(A.Decision().WithId("d-1").Build(), CancellationToken.None);
        var cli = new JudgeCli(_state);
        _ = await cli.Run("approve", "d-1");
        cli.Clock.Advance(TimeSpan.FromMinutes(5));

        var again = await cli.Run("approve", "d-1");

        Assert.Equal(0, again.ExitCode);
        Assert.Equal(KnownInstants.Epoch, again.Json().GetProperty("approved_at").GetDateTimeOffset());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Approve_exits_1_for_an_unknown_id()
    {
        var cli = new JudgeCli(_state);

        Assert.Equal(new CliRun(1, "", "unknown decision: nope\n"), await cli.Run("approve", "nope"));
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Approve_of_an_unreadable_row_exits_2_with_one_line_naming_the_decision()
    {
        await UnreadableRow.WithTs(_state, "d-bad", "yesterday");

        UnreadableRow.AssertNamed(await new JudgeCli(_state).Run("approve", "d-bad"), "d-bad", "approved");
    }

    public void Dispose() => _state.Dispose();
}
