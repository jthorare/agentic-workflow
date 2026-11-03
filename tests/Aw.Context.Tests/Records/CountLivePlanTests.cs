using Aw.Context.Infrastructure.Records;
using Aw.Context.Tests.Infrastructure;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Tests.Records;

/// <summary>
/// The concepts counts behind every index status read (#1951) must reach a record's chunk vectors through chunk_by_record and
/// the embedding key, never by walking every chunk vector for every record (records x vectors, hours on a real store).
/// </summary>
public sealed class CountLivePlanTests : IDisposable
{
    private readonly RecordStoreWorld _world = new();

    public void Dispose() => _world.Dispose();

    [Fact, Trait("Lane", "integration")]
    public async Task CountLive_finds_a_records_chunk_vectors_by_chunk_by_record_and_the_embedding_key()
    {
        await _world.Migrate();

        var plan = Plan();

        Assert.Contains(plan, line => line.StartsWith("SEARCH c ", StringComparison.Ordinal) && line.Contains("chunk_by_record (record=?)", StringComparison.Ordinal));
        Assert.Contains(plan, line => line.StartsWith("SEARCH e ", StringComparison.Ordinal) && line.Contains("(space=? AND key=?)", StringComparison.Ordinal));
        Assert.DoesNotContain(plan, line => line.StartsWith("SEARCH e ", StringComparison.Ordinal) && line.EndsWith("(space=?)", StringComparison.Ordinal));
    }

    private List<string> Plan()
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _world.DatabasePath, Pooling = false }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + RecordSql.CountLive;
        var lines = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            lines.Add(reader.GetString(3));
        }

        return lines;
    }
}
