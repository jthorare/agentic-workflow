namespace Aw.Embeddings.Tests.Codec;

[Trait("Lane", "unit")]
public sealed class VectorCodecTests
{
    [Fact]
    public void A_vector_round_trips_through_its_blob()
    {
        float[] vector = [0.25f, -1.5f, 3e-7f, 0];

        var blob = VectorCodec.Encode(vector);
        var decoded = VectorCodec.Decode(blob, 4);

        Assert.Equal(16, blob.Length);
        Assert.Equal(vector, decoded.Value);
    }

    [Fact]
    public void The_blob_is_little_endian_float32()
        => Assert.Equal(new byte[] { 0x00, 0x00, 0x80, 0x3F }, VectorCodec.Encode([1f]));

    [Theory]
    [InlineData(15, 4)]
    [InlineData(17, 4)]
    [InlineData(16, 3)]
    [InlineData(0, 0)]
    [InlineData(16, -4)]
    public void A_blob_of_the_wrong_length_is_refused(int bytes, int dimension)
    {
        var decoded = VectorCodec.Decode(new byte[bytes], dimension);

        Assert.Equal(EmbeddingsErrors.VectorInvalid.Code, decoded.Error.Code);
    }

    [Fact]
    public void HasDimension_checks_without_decoding()
    {
        Assert.True(VectorCodec.HasDimension(new byte[8], 2));
        Assert.False(VectorCodec.HasDimension(new byte[8], 3));
        Assert.False(VectorCodec.HasDimension([], 0));
    }

    [Fact]
    public void Normalize_scales_to_unit_length()
    {
        float[] vector = [3, 4];

        Assert.True(VectorMath.Normalize(vector));

        Assert.Equal(0.6f, vector[0], 1e-6f);
        Assert.Equal(0.8f, vector[1], 1e-6f);
    }

    [Fact]
    public void Normalize_leaves_a_zero_vector_alone_and_says_so()
    {
        float[] vector = [0, 0, 0];

        Assert.False(VectorMath.Normalize(vector));

        Assert.Equal(new float[] { 0, 0, 0 }, vector);
    }

    [Fact]
    public void Normalize_refuses_a_non_finite_vector()
    {
        float[] vector = [1, float.PositiveInfinity];

        Assert.False(VectorMath.Normalize(vector));
    }
}
