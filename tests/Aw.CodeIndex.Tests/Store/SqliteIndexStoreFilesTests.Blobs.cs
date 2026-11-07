using System.Globalization;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Similarity;
using Aw.CodeIndex.Core.Values;
using Aw.CodeIndex.Infrastructure.Store;
using Microsoft.Data.Sqlite;

namespace Aw.CodeIndex.Tests.Store;

/// <summary>A blob column that holds something other than a blob is damage, never a bare cast exception (#2242).</summary>
public sealed partial class SqliteIndexStoreFilesTests
{
    [Fact]
    public async Task A_subject_minhash_that_is_not_a_blob_is_damage()
    {
        var store = Store();
        await PublishTwins(store);
        await Plant("UPDATE symbols SET minhash = 'not a blob' WHERE ref = $ref", Twin("A.Run").Ref.Value);

        var reader = (await store.OpenRead(_repo, _none)).Value;
        await using (reader)
        {
            Assert.Equal("SND-CODEIDX-010", (await reader.CloneCandidates(Twin("A.Run").Ref, _none)).Error.Code);
        }
    }

    [Fact]
    public async Task A_banded_candidate_minhash_that_is_not_a_blob_is_damage()
    {
        var store = Store();
        await PublishTwins(store);
        await Plant("UPDATE symbols SET minhash = 'not a blob' WHERE ref = $ref", Twin("A.Copy").Ref.Value);

        var reader = (await store.OpenRead(_repo, _none)).Value;
        await using (reader)
        {
            Assert.Equal("SND-CODEIDX-010", (await reader.CloneCandidates(Twin("A.Run").Ref, _none)).Error.Code);
        }
    }

    [Fact]
    public async Task A_vector_that_is_not_a_blob_is_damage()
    {
        var store = Store();
        await PublishTwins(store);
        await Plant("UPDATE embeddings SET vector = 'not a blob'", value: null);

        var reader = (await store.OpenRead(_repo, _none)).Value;
        await using (reader)
        {
            Assert.Equal("SND-CODEIDX-010", (await reader.Nearest(new float[] { 1, 0 }, 2, _none)).Error.Code);
        }
    }

    [Fact]
    public async Task A_symbol_vector_that_is_not_a_blob_is_damage()
    {
        var store = Store();
        await PublishTwins(store);
        await Plant("UPDATE embeddings SET vector = 'not a blob'", value: null);

        var reader = (await store.OpenRead(_repo, _none)).Value;
        await using (reader)
        {
            Assert.Equal("SND-CODEIDX-010", (await reader.SymbolVectors(["src/A.cs"], "m", _none)).Error.Code);
        }
    }

    private static SymbolRecord Twin(string qualified)
    {
        var minHash = Clones.Signature([.. Enumerable.Range(0, 40).Select(i => "t" + i.ToString(CultureInfo.InvariantCulture))]);
        return Symbol("src/A.cs", qualified) with { MinHash = minHash, BandKeys = Clones.BandKeys(minHash) };
    }

    private static async Task PublishTwins(SqliteIndexStore store)
    {
        var build = (await store.BeginBuild(_repo, incremental: false, _none)).Value;
        await using (build)
        {
            Assert.True((await build.WriteFiles([FileOf("src/A.cs", Twin("A.Run"), Twin("A.Copy") with { StartLine = 10, EndLine = 15 })], _none)).IsSuccess);
            Assert.True((await build.WriteEmbeddings([new EmbeddingRecord(Twin("A.Run").Ref, "m", new float[] { 1, 0 })], _none)).IsSuccess);
            Assert.True((await build.Publish(new IndexMeta(CommitSha.From(new string('a', 40)), "main", _now, CodeIndexRules.SchemaVersion, "parser-1", LockSha: null, new IndexCounts(0, 0, 0)), _none)).IsSuccess);
        }
    }

    private async Task Plant(string sql, string? value)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = LiveFile, Pooling = false }.ToString());
        await using (connection)
        {
            await connection.OpenAsync(_none);
            var command = connection.CreateCommand();
            command.CommandText = sql;
            if (value is not null)
            {
                _ = command.Parameters.AddWithValue("$ref", value);
            }

            Assert.Equal(1, await command.ExecuteNonQueryAsync(_none));
        }
    }
}
