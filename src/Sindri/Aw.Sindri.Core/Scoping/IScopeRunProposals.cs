using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Scoping;

/// <summary>A scoping run's proposals on the context map as the Steps write them (scoping spec C9): the run's surfaces replace its earlier ones, and none withdraws them.</summary>
public interface IScopeRunProposals
{
    /// <summary>Writes the run's surfaces as draft proposals; the empty list withdraws the run's undecided ones.</summary>
    /// <param name="run">The run, which is the instance's address.</param>
    /// <param name="repo">The repo the surfaces were found in.</param>
    /// <param name="surfaces">The surfaces; none withdraws.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Where each submitted surface stands, in order (none for a withdrawal), or the error.</returns>
    public ValueTask<Result<IReadOnlyList<ScopeSurfaceStatus>>> Replace(InstanceAddress run, RepoName repo, IReadOnlyList<ProposedScopeSurface> surfaces, CancellationToken cancellationToken);
}
