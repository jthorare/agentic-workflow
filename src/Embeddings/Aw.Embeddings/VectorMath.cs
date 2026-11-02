namespace Aw.Embeddings;

/// <summary>Vector arithmetic the store needs.</summary>
public static class VectorMath
{
    /// <summary>Scales a vector to unit length (L2), in place.</summary>
    /// <param name="vector">The vector.</param>
    /// <returns>False, with the vector untouched, when its length is zero or not a finite number.</returns>
    public static bool Normalize(Span<float> vector)
    {
        double sum = 0;
        foreach (var x in vector)
        {
            sum += (double)x * x;
        }

        var norm = Math.Sqrt(sum);
        if (norm == 0 || !double.IsFinite(norm))
        {
            return false;
        }

        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] = (float)(vector[i] / norm);
        }

        return true;
    }
}
