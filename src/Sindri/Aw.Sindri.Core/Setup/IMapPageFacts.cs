using Aw.Kernel;

namespace Aw.Sindri.Core.Setup;

/// <summary>Asks the current published map what a page write checks against (pages spec §3.1). The context slice implements it over the engine.</summary>
public interface IMapPageFacts
{
    /// <summary>Reads the facts of the current map.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The facts (empty, with a null version, when there is no map yet), or the engine's error.</returns>
    public ValueTask<Result<MapPageFacts>> Read(CancellationToken cancellationToken);
}
