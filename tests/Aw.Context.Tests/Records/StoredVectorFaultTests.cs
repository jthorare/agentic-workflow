using Aw.Context.Core.Records;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Infrastructure;
using Aw.Kernel.Data;
using Microsoft.Data.Sqlite;

namespace Aw.Context.Tests.Records;

/// <summary>A stored record vector that is a ragged blob or off its dim is damage the store refuses on read, never garbage floats; text never gets in, the column refuses it (#2253).</summary>
[Trait("Lane", "integration")]
public sealed class StoredVectorFaultTests : IDisposable
{
    private readonly RecordStoreWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // why: two floats are 8 bytes and dim 2, so the stored vector is valid until a test damages it.
    private async Task StoreOne()
    {
        var stored = await _world.Store.PutVectors([new VectorWrite(VectorSpace.Node, "feature:billing", "fake-model@1", RecordSamples.Hash("billing"), new float[] { 1, 0 })], Ct);
        Assert.True(stored.IsSuccess);
    }

    private async Task AssertRefused()
    {
        var thrown = await Assert.ThrowsAnyAsync<Exception>(async () => await _world.Store.Vectors(VectorSpace.Node, 0, 10, Ct));

        Assert.True(thrown is InvalidDataException or StoredRowException, thrown.ToString());
    }

    [Fact]
    public async Task The_vector_column_refuses_text_so_no_text_can_be_read_back()
    {
        await StoreOne();

        var refused = Assert.Throws<SqliteException>(() => _world.Sql("UPDATE embedding SET vec = 'abcdefgh'"));

        Assert.Equal(19, refused.SqliteErrorCode);
    }

    [Fact]
    public async Task A_vector_blob_that_is_not_whole_floats_is_damage_not_garbage_floats()
    {
        await StoreOne();
        _world.Sql("UPDATE embedding SET vec = X'0000803F00'");

        await AssertRefused();
    }

    [Fact]
    public async Task A_vector_blob_that_does_not_match_its_dim_is_damage_not_garbage_floats()
    {
        await StoreOne();
        _world.Sql("UPDATE embedding SET dim = dim + 1");

        await AssertRefused();
    }
}
