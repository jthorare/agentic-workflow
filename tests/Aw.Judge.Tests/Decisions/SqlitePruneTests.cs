using System.Diagnostics;
using Aw.Judge.Infrastructure.Sqlite;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;
using Microsoft.Data.Sqlite;

namespace Aw.Judge.Tests.Decisions;

/// <summary>The SQLite prune reads before it writes: with nothing to drop it never takes the write lock.</summary>
[Lane(TestLane.Integration)]
public sealed class SqlitePruneTests : IDisposable
{
    private readonly TempStateDirectory _state = new();

    [Fact]
    public async Task Pruning_with_nothing_old_enough_answers_at_once_while_another_writer_holds_the_lock()
    {
        var ct = TestContext.Current.CancellationToken;
        var log = _state.DecisionLog();
        Assert.True((await log.Record(A.Decision().At(KnownInstants.Epoch).Build(), ct)).IsSuccess);
        await using var writer = new SqliteConnection($"Data Source={Path.Combine(_state.Path, "judge", "decisions.sqlite")}");
        await writer.OpenAsync(ct);
        await using (var begin = writer.CreateCommand())
        {
            begin.CommandText = "BEGIN IMMEDIATE";
            _ = await begin.ExecuteNonQueryAsync(ct);
        }

        var clock = Stopwatch.StartNew();
        var pruned = await log.PruneDetailsBefore(KnownInstants.Epoch.AddDays(-1), ct);

        Assert.Equal(0, pruned.Value);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1), $"no wait for the busy timeout, took {clock.Elapsed}");
    }

    [Fact]
    public async Task The_prune_check_starts_from_the_details_table_so_its_cost_is_bounded_by_retention()
    {
        // Decision rows are never deleted, so a plan that starts from decisions walks every old row once the details
        // are gone; decision_details holds only the retention window.
        var ct = TestContext.Current.CancellationToken;
        var log = _state.DecisionLog();
        Assert.True((await log.Record(A.Decision().At(KnownInstants.Epoch).Build(), ct)).IsSuccess);
        await using var connection = new SqliteConnection($"Data Source={Path.Combine(_state.Path, "judge", "decisions.sqlite")};Mode=ReadOnly");
        await connection.OpenAsync(ct);
        await using var explain = connection.CreateCommand();
        explain.CommandText = "EXPLAIN QUERY PLAN " + DecisionSql.HasDetailsBefore;
        _ = explain.Parameters.AddWithValue("$before", "2000-01-01T00:00:00.0000000Z");

        List<string> plan = [];
        await using (var reader = await explain.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                plan.Add(reader.GetString(3));
            }
        }

        // The two table steps, in join order: d is decisions, x is decision_details.
        var steps = plan.Select(step => step.Split(' ')).Where(words => words is ["SCAN" or "SEARCH", "d" or "x", ..]).Select(words => words[1]).ToList();
        Assert.True(steps is ["x", "d"], string.Join(" | ", plan));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        _state.Dispose();
    }
}
