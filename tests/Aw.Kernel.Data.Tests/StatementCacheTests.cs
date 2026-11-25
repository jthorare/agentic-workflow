namespace Aw.Kernel.Data.Tests;

/// <summary>A <see cref="SqliteStatementCache"/> parses each statement once and runs it like a fresh command each time (#2890).</summary>
[Trait("Lane", "integration")]
public sealed class StatementCacheTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_statement_run_many_times_is_held_once_and_rebinds_its_values_each_run()
    {
        using var db = new TempDatabase();
        await Schema.CreateItems(db);
        await using var work = (await new SqliteUnitOfWorkFactory(db.Factory()).BeginSession(Ct)).Value;
        using var cache = new SqliteStatementCache(work);
        var items = new ItemRepository(cache);

        for (var i = 0; i < 50; i++)
        {
            Assert.True((await items.Add(new Item(new ItemId(string.Create(System.Globalization.CultureInfo.InvariantCulture, $"i{i:D2}")), string.Create(System.Globalization.CultureInfo.InvariantCulture, $"name {i}"), i), Ct)).IsSuccess);
        }

        var read = await SqliteQueries.List(cache, new SqlStatement("SELECT id, name, size FROM items ORDER BY id"), ItemMapper.Instance, Ct);
        Assert.Equal(2, cache.Count);
        Assert.Equal(50, read.Value.Count);
        Assert.Equal(new Item(new ItemId("i37"), "name 37", 37), read.Value[37]);
    }

    [Fact]
    public async Task A_held_statement_maps_a_constraint_failure_and_still_runs_after_it()
    {
        using var db = new TempDatabase();
        await Schema.CreateItems(db);
        await using var work = (await new SqliteUnitOfWorkFactory(db.Factory()).BeginSession(Ct)).Value;
        using var cache = new SqliteStatementCache(work);
        var items = new ItemRepository(cache);
        Assert.True((await items.Add(new Item(new ItemId("a"), "alpha", 1), Ct)).IsSuccess);

        var duplicate = await items.Add(new Item(new ItemId("a"), "again", 2), Ct);
        var next = await items.Add(new Item(new ItemId("b"), "beta", 2), Ct);

        Assert.Equal(DataErrors.Constraint.Code, duplicate.Error.Code);
        Assert.True(next.IsSuccess);
        Assert.Equal(1, cache.Count);
    }

    [Fact]
    public async Task Its_writes_join_the_inner_transaction_and_roll_back_with_it()
    {
        using var db = new TempDatabase();
        await Schema.CreateItems(db);
        var sessions = new SqliteUnitOfWorkFactory(db.Factory());
        await using (var work = (await sessions.BeginSession(Ct)).Value)
        {
            using var cache = new SqliteStatementCache(work);
            Assert.True((await new ItemRepository(cache).Add(new Item(new ItemId("a"), "alpha", 1), Ct)).IsSuccess);
        }

        await using var check = (await sessions.BeginSession(Ct)).Value;
        Assert.False((await SqliteQueries.Exists(check, new SqlStatement("SELECT EXISTS (SELECT 1 FROM items)"), Ct)).Value);
    }
}
