using Aw.Kernel;

namespace Aw.Context.Core.Vectors;

/// <summary>The in-memory matrix of every stored chunk vector of one model (spec §4.4), kept up to date from the store's counters.</summary>
public interface IVectorMatrixCache
{
    /// <summary>Returns the matrix, loading only what is new since the last call; it is rebuilt whole when a vector was deleted or the model changed.</summary>
    /// <param name="model">The model name and digest; vectors of other models are not in the matrix.</param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <returns>A snapshot that later appends do not change; no vectors is an empty matrix.</returns>
    public ValueTask<Result<VectorMatrix>> Current(string model, CancellationToken cancellationToken);
}
