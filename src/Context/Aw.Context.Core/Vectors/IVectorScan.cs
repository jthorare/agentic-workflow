using Aw.Kernel;

namespace Aw.Context.Core.Vectors;

/// <summary>The nearest rows of a matrix to a query (spec §4.4); the Host binds it to 'Aw.Embeddings'' in-process scan.</summary>
public interface IVectorScan
{
    /// <summary>Finds the best rows. The scope mask applies inside the scan, before the top rows are taken, never after it.</summary>
    /// <param name="matrix">The rows.</param>
    /// <param name="query">The query vector, L2-normalised, of the matrix's dimension.</param>
    /// <param name="mask">One flag per row, true to consider it; empty to consider every row.</param>
    /// <param name="count">How many to return, 1 to 1,000.</param>
    /// <returns>At most <paramref name="count"/> hits, best score first, ties in row order; a query or mask of the wrong size is 'SND-CONTEXT-232'.</returns>
    public Result<IReadOnlyList<VectorHit>> Top(VectorMatrix matrix, ReadOnlyMemory<float> query, ReadOnlyMemory<bool> mask, int count);
}
