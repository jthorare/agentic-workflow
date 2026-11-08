using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Judge;
using Aw.Sindri.Core.Scoping;

namespace Aw.Sindri.Infrastructure.Judge;

/// <summary>
/// <see cref="IStepImageRoots"/> over the approved profile and the environment: the instance repo's checkout, and ui-evidence's output root,
/// $AW_STATE_DIR/ui-evidence (its runs are runs/{id} under it), else ~/.agentic-workflow/ui-evidence.
/// </summary>
/// <param name="checkouts">Where a repo is checked out.</param>
/// <param name="environment">Reads AW_STATE_DIR.</param>
/// <param name="files">Resolves each root to its real path, so an image is compared with symlinks followed on both sides.</param>
internal sealed class StepImageRoots(IRepoCheckouts checkouts, Func<string, string?> environment, IStepImageFiles files) : IStepImageRoots
{
    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<string>> Of(RepoName? repo, CancellationToken cancellationToken)
    {
        List<string> roots = [];
        if (repo is { } name && await checkouts.Of(name, cancellationToken).ConfigureAwait(false) is { IsSuccess: true } checkout)
        {
            roots.Add(checkout.Value);
        }

        roots.Add(Path.Combine(SettingsFromEnvironment.StateRoot(environment), "ui-evidence"));
        return [.. roots.Select(files.RealDirectory)];
    }
}
