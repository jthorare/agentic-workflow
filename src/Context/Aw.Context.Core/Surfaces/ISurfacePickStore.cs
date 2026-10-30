using Aw.Kernel;

namespace Aw.Context.Core.Surfaces;

/// <summary>Jev's decisions on product-surface groups, by the group's key, so a rebuild asks the model only about groups it has not decided (q149).</summary>
public interface ISurfacePickStore
{
    /// <summary>Finds the decisions saved under these group keys.</summary>
    /// <param name="groupKeys">The <see cref="SurfaceGroup.Key"/> values to look up.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The decision of each key that is saved (a missing key is absent), or the error.</returns>
    public ValueTask<Result<IReadOnlyDictionary<string, SurfaceDecision>>> Get(IReadOnlyCollection<string> groupKeys, CancellationToken cancellationToken);

    /// <summary>Saves decisions, replacing a group key that is already saved.</summary>
    /// <param name="decisions">The decisions to save.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>Success, or the error.</returns>
    public ValueTask<Result<Unit>> Save(IReadOnlyList<SurfaceDecision> decisions, CancellationToken cancellationToken);
}
