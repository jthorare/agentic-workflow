using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Projects.Facts;

namespace Aw.Sindri.Features.ProjectFacts;

/// <summary>
/// The facts port until Context's IContextProjects lands (projects page spec Ph2): every read is SND-PROJECT-002, so the page shows attached and
/// hand-made cards by name only (§3.1 state 3). Replaced by the adapter over IContextEngine.Projects when Ph2 merges.
/// </summary>
internal sealed class NoProjectFacts : IProjectFacts
{
    private static readonly Error _unavailable = SindriErrors.ProjectFactsUnavailable.With("this build has no Linear project feed yet");

    public ValueTask<Result<ProjectFactsSnapshot>> Read(IReadOnlyList<string> attached, CancellationToken cancellationToken) => ValueTask.FromResult<Result<ProjectFactsSnapshot>>(_unavailable);

    public ValueTask<Result<IReadOnlyList<ProjectSourceFreshness>>> Freshness(CancellationToken cancellationToken) => ValueTask.FromResult<Result<IReadOnlyList<ProjectSourceFreshness>>>(_unavailable);
}
