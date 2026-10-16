using Aw.Evidence.Core;
using Aw.Evidence.Core.Data;
using Xunit;

namespace Aw.Evidence.Testing;

/// <summary>
/// The IDatabaseReader contract: rows read back with their types, the row limit cuts and says so, and every way of writing or of
/// escaping read-only (a writing statement, a second statement, a function with a side effect, a pragma, a new file) is refused with
/// AW-EVIDENCE-020, leaves the data and the files beside it unchanged and leaves the session usable. The adapter is called
/// directly, so the pre-run check does not stand in for the connection.
/// </summary>
public abstract class ReadOnlyDatabaseContract
{
    /// <summary>Reads every note, in order.</summary>
    public const string ReadAll = "SELECT id, text FROM notes ORDER BY id";

    /// <summary>Reads the first note with a null and a decimal beside it.</summary>
    public const string ReadTyped = "SELECT id, text, NULL AS gone, 1.5 AS ratio FROM notes ORDER BY id LIMIT 1";

    /// <summary>A statement for a table that is not there.</summary>
    public const string ReadMissing = "SELECT * FROM missing_table";

    /// <summary>Arranges a database whose notes table holds (1, 'hello'), (2, 'world'), (3, 'again').</summary>
    /// <returns>The reader, the target and the snapshot.</returns>
    protected abstract Task<DatabaseUnderTest> ArrangeAsync();

    /// <summary>The statements this engine must refuse, by name: each a way to write or to escape read-only that the connection stops.</summary>
    /// <returns>The name and the SQL of each.</returns>
    protected abstract IReadOnlyList<(string Name, string Sql)> Bypasses();

    /// <summary>Rows read back in order, with null and numbers typed.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Rows_read_back_in_order_with_their_types()
    {
        var db = await ArrangeAsync();
        await using var session = (await db.Reader.OpenAsync(db.Target, CancellationToken.None)).Value;

        var all = await session.QueryAsync(ReadAll, 10, CancellationToken.None);
        var typed = await session.QueryAsync(ReadTyped, 10, CancellationToken.None);

        Expect.That(all.IsSuccess && all.Value.ToJson() == """[{"id":1,"text":"hello"},{"id":2,"text":"world"},{"id":3,"text":"again"}]""", "the rows read back in order as JSON");
        Expect.That(typed.IsSuccess && typed.Value.ToJson() == """[{"id":1,"text":"hello","gone":null,"ratio":1.5}]""", "null and numbers keep their type");
    }

    /// <summary>The row limit keeps that many rows and says there were more.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task The_row_limit_keeps_that_many_rows_and_says_it_cut()
    {
        var db = await ArrangeAsync();
        await using var session = (await db.Reader.OpenAsync(db.Target, CancellationToken.None)).Value;

        var cut = await session.QueryAsync(ReadAll, 2, CancellationToken.None);
        var whole = await session.QueryAsync(ReadAll, 3, CancellationToken.None);

        Expect.That(cut.IsSuccess && cut.Value.Count == 2 && cut.Value.Truncated, "a limit of 2 keeps 2 of 3 rows and says it cut");
        Expect.That(whole.IsSuccess && whole.Value.Count == 3 && !whole.Value.Truncated, "a limit equal to the rows cuts nothing");
    }

    /// <summary>Every bypass is refused, leaves everything as it was, and the session still answers.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task Every_bypass_is_refused_and_leaves_everything_unchanged()
    {
        var db = await ArrangeAsync();
        await using var session = (await db.Reader.OpenAsync(db.Target, CancellationToken.None)).Value;
        _ = await session.QueryAsync(ReadAll, 10, CancellationToken.None);
        var before = await db.SnapshotAsync();

        foreach (var (name, sql) in Bypasses())
        {
            var result = await session.QueryAsync(sql, 10, CancellationToken.None);
            Expect.That(result.IsError && result.Error.Code == EvidenceErrors.DatabaseRefused.Code, $"{name} is refused with AW-EVIDENCE-020 (got {Describe(result)})");
            Expect.That(await db.SnapshotAsync() == before, $"{name} changes nothing");
        }

        var after = await session.QueryAsync(ReadAll, 10, CancellationToken.None);
        Expect.That(after.IsSuccess && after.Value.Count == 3, "the session still answers after the refusals");
    }

    /// <summary>A second statement is refused, whatever it is.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_second_statement_is_refused()
    {
        var db = await ArrangeAsync();
        await using var session = (await db.Reader.OpenAsync(db.Target, CancellationToken.None)).Value;

        var result = await session.QueryAsync("SELECT 1; SELECT 2", 10, CancellationToken.None);

        Expect.That(result.IsError && result.Error.Code == EvidenceErrors.DatabaseRefused.Code, $"two statements are refused with AW-EVIDENCE-020 (got {Describe(result)})");
    }

    /// <summary>A statement that fails for another reason is not a refusal.</summary>
    /// <returns>A task.</returns>
    [Fact]
    public async Task A_statement_that_fails_for_another_reason_is_a_run_failure_not_a_refusal()
    {
        var db = await ArrangeAsync();
        await using var session = (await db.Reader.OpenAsync(db.Target, CancellationToken.None)).Value;

        var result = await session.QueryAsync(ReadMissing, 10, CancellationToken.None);

        Expect.That(result.IsError && result.Error.Code == EvidenceErrors.RunFailed.Code, $"a missing table is AW-EVIDENCE-015 (got {Describe(result)})");
    }

    private static string Describe(Kernel.Result<Rows> result) => result.IsError ? result.Error.Code + " " + result.Error.Message : "success";
}
