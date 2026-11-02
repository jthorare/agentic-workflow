namespace Aw.Kernel.Data.Tests;

/// <summary>SqliteRead.GetTimestamp reads back the UTC round-trip text SqliteBind writes and any other ISO-8601 UTC text (the TS judge writes milliseconds); text without the Z is not a timestamp.</summary>
[Trait("Lane", "integration")]
public sealed class TimestampReadTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetTimestamp_reads_utc_round_trip_text_at_offset_zero()
    {
        var read = await Read("2026-10-10T07:05:07.0420000Z", reader => SqliteRead.GetTimestamp(reader, 0));

        Assert.Equal(new DateTimeOffset(2026, 10, 10, 7, 5, 7, 42, TimeSpan.Zero), read);
        Assert.Equal(TimeSpan.Zero, read.Offset);
    }

    [Theory]
    [InlineData("2026-10-26T01:08:26.971Z", "2026-10-26T01:08:26.9710000Z")]
    [InlineData("2026-10-10T07:05:07Z", "2026-10-10T07:05:07.0000000Z")]
    [InlineData("2026-10-10T07:05:07.5Z", "2026-10-10T07:05:07.5000000Z")]
    public async Task GetTimestamp_reads_any_iso_8601_utc_text_such_as_the_ts_judge_s_milliseconds(string stored, string roundTrip)
    {
        var read = await Read(stored, reader => SqliteRead.GetTimestamp(reader, 0));

        Assert.Equal(DateTimeOffset.Parse(roundTrip, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal), read);
        Assert.Equal(TimeSpan.Zero, read.Offset);
    }

    [Theory]
    [InlineData("2026-10-10T07:05:07.0420000+00:00")]
    [InlineData("2026-10-10T07:05:07.0420000")]
    [InlineData("not a time")]
    public async Task GetTimestamp_rejects_any_other_text(string stored) =>
        await Assert.ThrowsAsync<InvalidDataException>(() => Read(stored, reader => SqliteRead.GetTimestamp(reader, 0)));

    private static async Task<T> Read<T>(string stored, Func<Microsoft.Data.Sqlite.SqliteDataReader, T> read)
    {
        using var db = new TempDatabase();
        await using var connection = (await db.Factory().Open(Ct)).Value;
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT $value";
        _ = command.Bind("$value", stored);
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        return read(reader);
    }
}
