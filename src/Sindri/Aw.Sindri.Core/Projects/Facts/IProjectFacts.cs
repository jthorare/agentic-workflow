using Aw.Kernel;

namespace Aw.Sindri.Core.Projects.Facts;

/// <summary>
/// What the owned index knows about Linear projects (projects page spec §6.1), in Sindri's own records: Context's IContextProjects behind a
/// Features adapter, so Core never names the engine's types. Reads only stored data: no Linear or GitHub call.
/// </summary>
public interface IProjectFacts
{
    /// <summary>The projects the owner leads plus the attached ones the store holds, the catalog, who the owner is and the synced teams.</summary>
    /// <param name="attached">The attached Linear keys ('linear:project/&lt;id&gt;'); a key the store doesn't hold is left out of the answer, not an error.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The facts, or the error Context gave (the page then shows attached and hand-made cards by name only, spec §3.1 state 3).</returns>
    public ValueTask<Result<ProjectFactsSnapshot>> Read(IReadOnlyList<string> attached, CancellationToken cancellationToken);

    /// <summary>Each Linear and GitHub source's sync state, for the page's freshness line (the rows 'sindri index status' prints).</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>One row per source, or the error Context gave.</returns>
    public ValueTask<Result<IReadOnlyList<ProjectSourceFreshness>>> Freshness(CancellationToken cancellationToken);
}
