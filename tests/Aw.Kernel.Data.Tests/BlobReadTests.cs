namespace Aw.Kernel.Data.Tests;

/// <summary>SqliteRead.GetBlob reads a BLOB and nothing else: TEXT, an integer and NULL are data bugs that name the column (#2253).</summary>
[Trait("Lane", "integration")]
public sealed class BlobReadTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetBlob_returns_the_bytes_of_a_blob() =>
        Assert.Equal(new byte[] { 1, 2, 3 }, await Read("x'010203'"));

    [Theory]
    [InlineData("'abc'", "String")]
    [InlineData("7", "Int64")]
    [InlineData("NULL", "DBNull")]
    public async Task GetBlob_rejects_anything_but_a_blob_and_names_the_column(string literal, string type)
    {
        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => Read(literal));

        Assert.Contains("Column vec", failure.Message, StringComparison.Ordinal);
        Assert.Contains(type, failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("abc", failure.Message, StringComparison.Ordinal);
    }

    private static async Task<byte[]> Read(string literal)
    {
        using var db = new TempDatabase();
        await using var connection = (await db.Factory().Open(Ct)).Value;
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {literal} AS vec";
        await using var reader = await command.ExecuteReaderAsync(Ct);
        Assert.True(await reader.ReadAsync(Ct));
        return SqliteRead.GetBlob(reader, 0);
    }
}
