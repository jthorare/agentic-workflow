using Aw.Kernel;

namespace Aw.Sindri.Core.Setup;

/// <summary>Asks the current published map which feature keys it holds (concept pins spec §3.6: a pin may name only features that are in the map). The context slice implements it over the engine.</summary>
public interface IMapFeatures
{
    /// <summary>Looks up <paramref name="keys"/> in the current map.</summary>
    /// <param name="keys">Exact feature keys ('feature:billing.refunds').</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The check, or the engine's error other than "no map yet".</returns>
    public ValueTask<Result<MapFeatureCheck>> Check(IReadOnlyList<string> keys, CancellationToken cancellationToken);
}
