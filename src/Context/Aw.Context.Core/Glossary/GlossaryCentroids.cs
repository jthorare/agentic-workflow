namespace Aw.Context.Core.Glossary;

/// <summary>Centroids of features' symbol vectors.</summary>
internal static class GlossaryCentroids
{
    // The normalized mean of the feature's vectors. Vectors of another length than the first, and empty or zero ones, are skipped; null when none is left.
    internal static double[]? Unit(FeatureVectors feature)
    {
        var first = feature.Symbols.FirstOrDefault(s => !s.Vector.IsEmpty);
        if (first is null)
        {
            return null;
        }

        var sum = new double[first.Vector.Length];
        foreach (var symbol in feature.Symbols.Where(s => s.Vector.Length == sum.Length))
        {
            var span = symbol.Vector.Span;
            for (var i = 0; i < sum.Length; i++)
            {
                sum[i] += span[i];
            }
        }

        var norm = Math.Sqrt(sum.Sum(x => x * x));
        if (norm <= 0 || double.IsNaN(norm) || double.IsInfinity(norm))
        {
            return null;
        }

        return [.. sum.Select(x => x / norm)];
    }

    internal static double Dot(double[] a, double[] b)
    {
        if (a.Length != b.Length)
        {
            return 0;
        }

        var dot = 0.0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
        }

        return dot;
    }
}
