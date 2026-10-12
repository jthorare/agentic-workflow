using Aw.Judge.Testing;

namespace Aw.Judge.Tests.UndoDecision;

// The contract of 'judge undo': the row with undone_at set; an unknown id is the same error as why and approve.
public sealed class UndoDecisionCliTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task Undo_prints_the_row_with_undone_at_set_and_why_shows_it_after()
    {
        _ = await _state.DecisionLog().Record(A.Decision().WithId("d-1").Build(), CancellationToken.None);
        var cli = new JudgeCli(_state);
        cli.Clock.Advance(TimeSpan.FromMinutes(3));

        var run = await cli.Run("undo", "d-1");

        Assert.Equal((0, ""), (run.ExitCode, run.Stderr));
        DecisionJson.AssertRow(run.Json(), "d-1", "model", undone: true);
        Assert.Equal(cli.Clock.GetUtcNow(), run.Json().GetProperty("undone_at").GetDateTimeOffset());
        Assert.Equal(cli.Clock.GetUtcNow(), (await cli.Run("why", "d-1")).Json().GetProperty("undone_at").GetDateTimeOffset());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Undo_of_an_unknown_id_exits_1_with_unknown_decision()
    {
        var cli = new JudgeCli(_state);

        Assert.Equal(new CliRun(1, "", "unknown decision: nope\n"), await cli.Run("undo", "nope"));
    }

    public void Dispose() => _state.Dispose();
}
