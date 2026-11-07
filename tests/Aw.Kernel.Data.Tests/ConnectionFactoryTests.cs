using Microsoft.Data.Sqlite;

namespace Aw.Kernel.Data.Tests;

[Trait("Lane", "integration")]
public sealed class ConnectionFactoryTests
{
    private static async Task<string> Pragma(SqliteConnection connection, string name)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA {name}";
        return Convert.ToString(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture)!;
    }

    [Fact]
    public async Task Open_turns_on_wal_foreign_keys_and_the_busy_timeout()
    {
        using var db = new TempDatabase();

        var opened = await db.Factory(TimeSpan.FromMilliseconds(1500)).Open(TestContext.Current.CancellationToken);

        Assert.True(opened.IsSuccess);
        await using var connection = opened.Value;
        Assert.Equal("wal", await Pragma(connection, "journal_mode"));
        Assert.Equal("1", await Pragma(connection, "foreign_keys"));
        Assert.Equal("1500", await Pragma(connection, "busy_timeout"));
        Assert.Equal("1", await Pragma(connection, "synchronous"));
        Assert.Equal("4194304", await Pragma(connection, "journal_size_limit"));
    }

    [Fact]
    public async Task Open_in_a_missing_directory_is_unavailable_not_an_exception()
    {
        using var db = new TempDatabase();
        var factory = new SqliteConnectionFactory(new SqliteOptions(Path.Combine(db.Directory, "missing", "x.db")));

        var opened = await factory.Open(TestContext.Current.CancellationToken);

        Assert.True(opened.IsError);
        Assert.Equal(DataErrors.Unavailable.Code, opened.Error.Code);
        Assert.Equal(ErrorKind.Unavailable, opened.Error.Kind);
    }

    [Fact]
    public void Options_refuse_an_empty_path_or_a_non_positive_busy_timeout()
    {
        _ = Assert.Throws<ArgumentException>(() => new SqliteOptions(" "));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new SqliteOptions("x.db") { BusyTimeout = TimeSpan.Zero });
    }
}
