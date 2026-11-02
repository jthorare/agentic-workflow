using Aw.Context.Core;
using Aw.Context.Core.Vectors;
using Aw.Embeddings;
using Aw.Kernel;

namespace Aw.Sindri.Host.ContextVectors;

/// <summary>Context's <see cref="IVectorScan"/> over <see cref="IMatrixScan"/>: the matrix and mask go to the flat scan, its row hits come back with the key of each row (spec §4.4).</summary>
internal sealed class EmbeddingsVectorScan(IMatrixScan scan) : IVectorScan
{
    public Result<IReadOnlyList<VectorHit>> Top(VectorMatrix matrix, ReadOnlyMemory<float> query, ReadOnlyMemory<bool> mask, int count)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        if (query.Length != matrix.Dim || (matrix.Dim > 0 && matrix.Data.Length != matrix.Dim * matrix.Keys.Count))
        {
            return ContextErrors.VectorScanInvalid.With("the query or the matrix doesn't fit the matrix's dimension");
        }

        var scanned = scan.Top(matrix.Data, query, mask, count);
        if (scanned.IsError)
        {
            return scanned.Error.Code == EmbeddingsErrors.VectorInvalid.Code
                ? ContextErrors.VectorScanInvalid.With(scanned.Error.Message)
                : scanned.Error;
        }

        var hits = new List<VectorHit>(scanned.Value.Count);
        foreach (var hit in scanned.Value)
        {
            hits.Add(new VectorHit(hit.Row, matrix.Keys[hit.Row], hit.Score));
        }

        return hits;
    }
}
