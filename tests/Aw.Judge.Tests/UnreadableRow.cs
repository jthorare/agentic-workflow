using Aw.Judge.Infrastructure.Sqlite;
using Aw.Judge.Testing;
using Aw.Kernel.Data;

namespace Aw.Judge.Tests;

/// <summary>Rewrites a stored decision's ts the way another writer could have left it, so the row can't be read.</summary>
internal static class UnreadableRow
{
    internal static async Task WithTs(TempStateDirectory state, string id, string ts)
    {
        var ct = TestContext.Current.CancellationToken;
        _ = await state.DecisionLog().Record(A.Decision().WithId(id).Build(), ct);
        var factory = new SqliteConnectionFactory(JudgeDatabase.OptionsFor(Path.Combine(state.Path, "judge")));
        await using var connection = (await factory.Open(ct)).Value;
        await using var update = connection.CreateCommand();
        update.CommandText = "UPDATE decisions SET ts = $ts WHERE id = $id";
        _ = update.Bind("$ts", ts);
        _ = update.Bind("$id", id);
        _ = await update.ExecuteNonQueryAsync(ct);
    }

    internal static void AssertNamed(CliRun run, string id, string took)
    {
        Assert.Equal(2, run.ExitCode);
        Assert.Equal("", run.Stdout);
        var line = Assert.Single(run.Stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains(id, line, StringComparison.Ordinal);
        Assert.Contains(took, line, StringComparison.Ordinal);   // the row was marked before the read failed: say so, or someone retries
        Assert.Contains("ts", line, StringComparison.Ordinal);
    }
}
