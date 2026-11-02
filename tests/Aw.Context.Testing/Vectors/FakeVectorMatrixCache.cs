using Aw.Context.Core.Records;
using Aw.Context.Core.Vectors;
using Aw.Kernel;

namespace Aw.Context.Testing.Vectors;

/// <summary>An <see cref="IVectorMatrixCache"/> that rebuilds the matrix of one model from the store's chunk vectors on every call: no counters, nothing kept.</summary>
public sealed class FakeVectorMatrixCache(IRecordStore store) : IVectorMatrixCache
{
    /// <inheritdoc />
    public async ValueTask<Result<VectorMatrix>> Current(string model, CancellationToken cancellationToken)
    {
        var rows = await store.Vectors(VectorSpace.Chunk, 0, 10_000, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var mine = rows.Value.Where(r => r.Vector.Model == model).Select(r => r.Vector).ToList();
        var dim = mine.Count == 0 ? 0 : mine[0].Vec.Length;
        var data = mine.Where(v => v.Vec.Length == dim).SelectMany(v => v.Vec.ToArray()).ToArray();
        return new VectorMatrix(dim, data, [.. mine.Where(v => v.Vec.Length == dim).Select(v => v.Key)]);
    }
}
