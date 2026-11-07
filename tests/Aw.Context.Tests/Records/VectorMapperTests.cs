using Aw.Context.Core.Records;
using Aw.Context.Infrastructure.Records;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Infrastructure;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Tests.Records;

/// <summary>The record vector mapper refuses a vec that is not a blob of exactly dim floats as a StoredRowException naming embedding.vec and the row's key, never the bytes (#2253).</summary>
[Trait("Lane", "integration")]
public sealed class VectorMapperTests : IDisposable
{
    private readonly RecordStoreWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // why: the embedding table is STRICT and refuses a TEXT vec, so the TEXT fact comes from a plain SELECT on a plain connection, the six columns the mapper reads.
    private static StoredRowException MapFrom(string vecLiteral, int dim)
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var command = connection.CreateCommand();
#pragma warning disable CA2100 // test-only literals
        command.CommandText = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"SELECT 1 AS seq, 'feature:billing' AS key, 'm' AS model, 'h' AS text_hash, {dim} AS dim, {vecLiteral} AS vec");
#pragma warning restore CA2100
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());

        return Assert.Throws<StoredRowException>(() => RecordMappers.Vector(VectorSpace.Node).Map(reader));
    }

    [Theory]
    [InlineData("'abcdefgh'", 2)]
    [InlineData("X'0000803F00'", 2)]
    [InlineData("X'0000803F00000000'", 3)]
    [InlineData("X'0000803F00000000'", 0)]
    [InlineData("X'0000803F00000000'", int.MaxValue)]
    public void A_vec_that_is_not_a_blob_of_dim_floats_is_a_StoredRowException_naming_the_read(string vec, int dim)
    {
        var thrown = MapFrom(vec, dim);

        Assert.Equal("embedding", thrown.Table);
        Assert.Equal("vec", thrown.Column);
        Assert.Equal("feature:billing", thrown.RowKey);
        Assert.DoesNotContain("abcdefgh", thrown.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_ragged_blob_in_the_store_is_a_StoredRowException_from_Vectors()
    {
        var stored = await _world.Store.PutVectors([new VectorWrite(VectorSpace.Node, "feature:billing", "fake-model@1", RecordSamples.Hash("billing"), new float[] { 1, 0 })], Ct);
        Assert.True(stored.IsSuccess);
        _world.Sql("UPDATE embedding SET vec = X'0000803F00'");

        var thrown = await Assert.ThrowsAsync<StoredRowException>(async () => await _world.Store.Vectors(VectorSpace.Node, 0, 10, Ct));

        Assert.Equal("embedding", thrown.Table);
        Assert.Equal("vec", thrown.Column);
        Assert.Equal("feature:billing", thrown.RowKey);
    }
}
