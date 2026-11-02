using Aw.Judge.Infrastructure.Sqlite;
using Aw.Judge.Testing;
using Aw.Kernel.Data;

namespace Aw.Judge.Tests.ExplainDecision;

// 'judge why' on a row the TS judge wrote (milliseconds, 'Z') shows the decision; a row it cannot read is exit 2 and one stderr line.
public sealed class ExplainDecisionTimestampTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact, Trait("Lane", "integration")]
    public async Task Why_shows_a_decision_whose_ts_is_the_ts_judge_s_milliseconds_text()
    {
        await Record("d-ts", ts: "2026-10-26T01:08:26.971Z");

        var run = await new JudgeCli(_state).Run("why", "d-ts");

        Assert.Equal((0, ""), (run.ExitCode, run.Stderr));
        Assert.Equal("d-ts", run.Json().GetProperty("id").GetString());
    }

    [Fact, Trait("Lane", "integration")]
    public async Task Why_exits_2_with_one_stderr_line_naming_the_decision_and_the_column_when_ts_is_garbage()
    {
        await Record("d-bad", ts: "yesterday");

        var run = await new JudgeCli(_state).Run("why", "d-bad");

        Assert.Equal(2, run.ExitCode);
        Assert.Equal("", run.Stdout);
        var line = Assert.Single(run.Stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("d-bad", line, StringComparison.Ordinal);
        Assert.Contains("ts", line, StringComparison.Ordinal);
    }

    // Any other column of the row that holds text where the judge writes a number is the same case as a bad ts: named, exit 2.
    [Fact, Trait("Lane", "integration")]
    public async Task Why_exits_2_with_one_stderr_line_naming_the_decision_and_the_column_when_confidence_is_text()
    {
        await Record("d-conf", ts: "2026-10-26T01:08:26.971Z", column: "confidence", value: "high");

        var run = await new JudgeCli(_state).Run("why", "d-conf");

        Assert.Equal(2, run.ExitCode);
        Assert.Equal("", run.Stdout);
        var line = Assert.Single(run.Stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("d-conf", line, StringComparison.Ordinal);
        Assert.Contains("confidence", line, StringComparison.Ordinal);
    }

    // Records an ordinary decision, then rewrites its ts (or another column) the way another writer could have stored it.
    private async Task Record(string id, string ts, string column = "ts", string value = "")
    {
        var ct = TestContext.Current.CancellationToken;
        _ = await _state.DecisionLog().Record(A.Decision().WithId(id).Build(), ct);
        var factory = new SqliteConnectionFactory(JudgeDatabase.OptionsFor(Path.Combine(_state.Path, "judge")));
        await using var connection = (await factory.Open(ct)).Value;
        await using var update = connection.CreateCommand();
        update.CommandText = column == "ts" ? "UPDATE decisions SET ts = $ts WHERE id = $id" : $"UPDATE decisions SET ts = $ts, {column} = $value WHERE id = $id";
        _ = update.Bind("$ts", ts);
        if (column != "ts")
        {
            _ = update.Bind("$value", value);
        }

        _ = update.Bind("$id", id);
        _ = await update.ExecuteNonQueryAsync(ct);
    }

    public void Dispose() => _state.Dispose();
}
