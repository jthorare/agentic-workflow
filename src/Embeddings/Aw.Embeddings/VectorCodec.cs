using System.Buffers.Binary;
using Aw.Kernel;

namespace Aw.Embeddings;

/// <summary>The stored form of a vector: float32, little-endian, no header (the dimension lives beside the blob).</summary>
public static class VectorCodec
{
    private const int BytesPerFloat = sizeof(float);

    /// <summary>Encodes a vector.</summary>
    /// <param name="vector">The vector.</param>
    /// <returns>Four bytes per component.</returns>
    public static byte[] Encode(ReadOnlySpan<float> vector)
    {
        var blob = new byte[vector.Length * BytesPerFloat];
        for (var i = 0; i < vector.Length; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(blob.AsSpan(i * BytesPerFloat), vector[i]);
        }

        return blob;
    }

    /// <summary>Decodes a vector.</summary>
    /// <param name="blob">The bytes <see cref="Encode"/> made.</param>
    /// <param name="dimension">The dimension the blob must have, at least 1.</param>
    /// <returns>The vector; AW-EMBEDDINGS-002 when the dimension is below 1 or the blob is not exactly dimension times four bytes.</returns>
    public static Result<float[]> Decode(ReadOnlySpan<byte> blob, int dimension)
    {
        if (dimension < 1 || blob.Length != (long)dimension * BytesPerFloat)
        {
            return EmbeddingsErrors.VectorInvalid.With("the vector blob does not match its dimension");
        }

        var vector = new float[dimension];
        for (var i = 0; i < dimension; i++)
        {
            vector[i] = BinaryPrimitives.ReadSingleLittleEndian(blob[(i * BytesPerFloat)..]);
        }

        return vector;
    }

    /// <summary>Checks that a blob has a dimension, without decoding it.</summary>
    /// <param name="blob">The bytes.</param>
    /// <param name="dimension">The expected dimension.</param>
    /// <returns>True when the blob is exactly dimension times four bytes and the dimension is at least 1.</returns>
    public static bool HasDimension(ReadOnlySpan<byte> blob, int dimension)
        => dimension >= 1 && blob.Length == (long)dimension * BytesPerFloat;
}
