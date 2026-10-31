using Aw.Context.Core.Pages;
using Aw.Kernel;

namespace Aw.Sindri.Features.SetPages;

/// <summary>Asks the current published map what a page write checks against: the concept ids and names, and the repos of each surface and action key (pages spec §3.1). The context slice implements it over the engine.</summary>
public interface IMapPageFacts
{
    /// <summary>Reads the facts of the current map.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The facts (empty, with a null version, when there is no map yet), or the engine's error.</returns>
    public ValueTask<Result<PageMapFacts>> Read(CancellationToken cancellationToken);
}
