using Aw.Context.Core;
using Aw.Context.Core.Vectors;
using Aw.Kernel;

namespace Aw.Context.Testing.Vectors;

/// <summary>An <see cref="IVectorScan"/> that scores every row with a plain dot product; it passes <see cref="VectorScanContract{TScan}"/>.</summary>
public sealed class FakeVectorScan : IVectorScan
{
    /// <inheritdoc />
    public Result<IReadOnlyList<VectorHit>> Top(VectorMatrix matrix, ReadOnlyMemory<float> query, ReadOnlyMemory<bool> mask, int count)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        var rows = matrix.Keys.Count;
        if (query.Length != matrix.Dim || matrix.Data.Length != rows * matrix.Dim || (!mask.IsEmpty && mask.Length != rows) || count is < 1 or > 1000)
        {
            return ContextErrors.VectorScanInvalid.With("the query, the mask or the count does not fit the matrix");
        }

        var data = matrix.Data.Span;
        var q = query.Span;
        var hits = new List<VectorHit>();
        for (var row = 0; row < rows; row++)
        {
            if (!mask.IsEmpty && !mask.Span[row])
            {
                continue;
            }

            var score = 0f;
            for (var d = 0; d < matrix.Dim; d++)
            {
                score += data[(row * matrix.Dim) + d] * q[d];
            }

            hits.Add(new VectorHit(row, matrix.Keys[row], score));
        }

        return hits.OrderByDescending(h => h.Score).ThenBy(h => h.Row).Take(count).ToList();
    }
}
