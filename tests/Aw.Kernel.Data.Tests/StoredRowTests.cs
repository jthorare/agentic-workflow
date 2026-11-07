using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data.Tests;

/// <summary>A stored row a mapper can't read leaves SqliteQueries as a StoredRowException naming what was read, never the stored value (#2229).</summary>
[Trait("Lane", "integration")]
public sealed class StoredRowTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<SqliteUnitOfWork> Seeded(TempDatabase db)
    {
        await Schema.CreateItems(db);
        var work = (await new SqliteUnitOfWorkFactory(db.Factory()).BeginSession(Ct)).Value;
        Assert.True((await new ItemRepository(work).Add(new Item(new ItemId("a"), "secret-name", 1), Ct)).IsSuccess);
        return work;
    }

    private static readonly SqlStatement _select = new("SELECT id, name, size FROM items ORDER BY id", Subject: "the items");

    private sealed class Throwing<T>(Func<Exception> make) : IRowMapper<T>
    {
        public T Map(SqliteDataReader reader) => throw make();
    }

    [Fact]
    public async Task List_wraps_a_non_sqlite_exception_from_the_mapper()
    {
        using var db = new TempDatabase();
        await using var work = await Seeded(db);

        var thrown = await Assert.ThrowsAsync<StoredRowException>(async () =>
            _ = await SqliteQueries.List(work, _select, new Throwing<Item>(() => new ArgumentException("bad value secret-name")), Ct));

        _ = Assert.IsType<ArgumentException>(thrown.InnerException);
        Assert.Contains("the items", thrown.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-name", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task One_wraps_a_format_exception_from_the_mapper()
    {
        using var db = new TempDatabase();
        await using var work = await Seeded(db);

        var thrown = await Assert.ThrowsAsync<StoredRowException>(async () =>
            _ = await SqliteQueries.One(work, _select, new Throwing<Item>(() => new FormatException("nope")), Ct));

        _ = Assert.IsType<FormatException>(thrown.InnerException);
    }

    [Fact]
    public async Task A_StoredRowException_from_the_mapper_passes_through_as_it_is()
    {
        using var db = new TempDatabase();
        await using var work = await Seeded(db);
        var own = new StoredRowException("items", "name", "a", statement: null, new FormatException());

        var thrown = await Assert.ThrowsAsync<StoredRowException>(async () =>
            _ = await SqliteQueries.One(work, _select, new Throwing<Item>(() => own), Ct));

        Assert.Same(own, thrown);
    }

    [Fact]
    public async Task The_column_helper_names_the_table_the_column_and_the_key_but_not_the_value()
    {
        using var db = new TempDatabase();
        await using var work = await Seeded(db);
        var mapper = new Parsing(reader => SqliteRead.Parse<int>(reader, 1, int.Parse, table: "items", rowKey: reader.GetString(0)));

        var thrown = await Assert.ThrowsAsync<StoredRowException>(async () => await SqliteQueries.One(work, _select, mapper, Ct));

        Assert.Equal("items", thrown.Table);
        Assert.Equal("name", thrown.Column);
        Assert.Equal("a", thrown.RowKey);
        Assert.StartsWith("items.name of row a can't be read", thrown.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-name", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_nullable_helper_reads_NULL_as_null_and_text_through_the_parser()
    {
        using var db = new TempDatabase();
        await using var work = await Seeded(db);
        var nulls = new Parsing(reader => SqliteRead.ParseOrNull<int>(reader, 0, int.Parse, "items"));
        var statement = new SqlStatement("SELECT NULL, '7'");

        var none = (await SqliteQueries.One(work, statement, nulls, Ct)).Value;
        var seven = (await SqliteQueries.One(work, statement, new Parsing(reader => SqliteRead.ParseOrNull<int>(reader, 1, int.Parse, "items")), Ct)).Value;

        Assert.Null(none);
        Assert.Equal(7, seven);
    }

    private sealed class Parsing(Func<SqliteDataReader, object?> read) : IRowMapper<object?>
    {
        public object? Map(SqliteDataReader reader) => read(reader);
    }
}
