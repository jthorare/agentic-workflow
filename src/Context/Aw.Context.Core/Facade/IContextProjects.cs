using Aw.Context.Core.Engine;
using Aw.Context.Core.Projects;
using Aw.Kernel;

namespace Aw.Context.Core.Facade;

/// <summary>The Projects page's reads (projects page spec §6.1): stored Linear projects with their issue counts. Nothing here calls Linear.</summary>
public interface IContextProjects
{
    /// <summary>Reads the attached projects and the catalog of every stored project the caller may see, with open-issue counts, the owner, the profile's teams and where the feeds stand.</summary>
    /// <param name="request">The attached project keys.</param>
    /// <param name="scope">The profile's settings and repos, and the caller: the audience rule applies as for any record read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The answer (a key the store doesn't hold is left out, not an error), or SND-CONTEXT-001/004.</returns>
    public ValueTask<Result<ProjectFactsAnswer>> Projects(ProjectFactsRequest request, ContextScope scope, CancellationToken cancellationToken);
}
