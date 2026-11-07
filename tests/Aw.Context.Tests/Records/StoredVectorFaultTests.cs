using Aw.Context.Core.Records;
using Aw.Context.Testing.Records;
using Aw.Context.Tests.Infrastructure;
using Aw.Kernel.Data;

namespace Aw.Context.Tests.Records;

/// <summary>A stored record vector that is text, a ragged blob or off its dim is damage the store refuses on read, never garbage floats (#2253).</summary>
[Trait("Lane", "integration")]
public sealed class StoredVectorFaultTests : IDisposable
{
    private readonly RecordStoreWorld _world = new();

    public void Dispose() => _world.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // why: two floats are 8 bytes and dim 2, so the length of an 8-byte text cell looks valid and only the cell's type can give it away.
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
    public async Task A_vector_stored_as_text_is_damage_not_garbage_floats()
    {
        await StoreOne();
        _world.Sql("UPDATE embedding SET vec = 'abcdefgh'");

        await AssertRefused();
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
