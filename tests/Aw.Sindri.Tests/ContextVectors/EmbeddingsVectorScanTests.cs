using Aw.Context.Core;
using Aw.Context.Core.Vectors;
using Aw.Context.Testing.Vectors;
using Aw.Embeddings;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Host.ContextVectors;

namespace Aw.Sindri.Tests.ContextVectors;

/// <summary>The adapter behind Context's vector scan (owned-index spec §4.4): the in-process scan with chunk keys and Context's error code.</summary>
[Lane(TestLane.Unit)]
public sealed class EmbeddingsVectorScanTests : VectorScanContract<IVectorScan>
{
    private static readonly bool[] _firstMasked = [false, true, true];
    private static readonly VectorMatrix _matrix = new(2, new float[] { 1, 0, 0.6f, 0.8f, 0, 1 }, ["a", "b", "c"]);

    protected override IVectorScan Create() => new EmbeddingsVectorScan(new ScanInProcess());

    [Fact]
    public void Hits_carry_the_key_of_their_row()
    {
        var hits = Create().Top(_matrix, new float[] { 1, 0 }, default, 2);

        Assert.Equal(2, hits.Value.Count);
        Assert.True(hits.Value[0].Row == 0 && hits.Value[0].Key == "a" && MathF.Abs(hits.Value[0].Score - 1f) < 1e-5f);
        Assert.True(hits.Value[1].Row == 1 && hits.Value[1].Key == "b" && MathF.Abs(hits.Value[1].Score - 0.6f) < 1e-5f);
    }

    [Fact]
    public void The_mask_is_passed_to_the_scan()
    {
        var hits = Create().Top(_matrix, new float[] { 1, 0 }, _firstMasked, 1);

        Assert.Equal("b", Assert.Single(hits.Value).Key);
    }

    [Fact]
    public void An_empty_matrix_has_no_hits()
    {
        var hits = Create().Top(new VectorMatrix(2, ReadOnlyMemory<float>.Empty, []), new float[] { 1, 0 }, default, 5);

        Assert.True(hits.IsSuccess);
        Assert.Empty(hits.Value);
    }

    [Fact]
    public void A_query_of_the_wrong_dimension_is_the_scan_invalid_error()
    {
        var hits = Create().Top(_matrix, new float[] { 1, 0, 0 }, default, 1);

        Assert.Equal(ContextErrors.VectorScanInvalid.Code, hits.Error.Code);
    }

    [Fact]
    public void A_count_out_of_range_is_mapped_from_the_embeddings_error()
    {
        var hits = Create().Top(_matrix, new float[] { 1, 0 }, default, 0);

        Assert.Equal(ContextErrors.VectorScanInvalid.Code, hits.Error.Code);
    }

    [Fact]
    public void Another_embeddings_error_keeps_its_code()
    {
        var other = new Error("AW-EMBEDDINGS-004", ErrorKind.Unavailable, "boom");

        var hits = new EmbeddingsVectorScan(new FailingScan(other)).Top(_matrix, new float[] { 1, 0 }, default, 1);

        Assert.Equal("AW-EMBEDDINGS-004", hits.Error.Code);
    }

    private sealed class FailingScan(Error error) : IMatrixScan
    {
        public Result<IReadOnlyList<ScanHit>> Top(ReadOnlyMemory<float> matrix, ReadOnlyMemory<float> query, ReadOnlyMemory<bool> mask, int count) => error;
    }
}
