using Aw.Kernel;

namespace Aw.Embeddings;

/// <summary>The nearest rows of a matrix to a query. The Host binds Context's vector-scan port to it.</summary>
public interface IMatrixScan
{
    /// <summary>The largest count a scan accepts.</summary>
    public const int MaxCount = 1000;

    /// <summary>Finds the best rows. The mask applies inside the scan, before the top rows are taken.</summary>
    /// <param name="matrix">Row-major, rows times query-length floats; may be empty.</param>
    /// <param name="query">The query vector, L2-normalised; its length is the dimension.</param>
    /// <param name="mask">One flag per row, true to consider it; empty to consider every row.</param>
    /// <param name="count">How many to return, 1 to <see cref="MaxCount"/>.</param>
    /// <returns>At most <paramref name="count"/> hits, best score first, ties in row order; AW-EMBEDDINGS-002 for a wrongly sized query, matrix, mask or count.</returns>
    public Result<IReadOnlyList<ScanHit>> Top(ReadOnlyMemory<float> matrix, ReadOnlyMemory<float> query, ReadOnlyMemory<bool> mask, int count);
}
