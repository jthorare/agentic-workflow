using Aw.Kernel;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine;

namespace Aw.Sindri.Core.Launches;

/// <summary>The read port over the 'launches' view (UI spec E-1): OpenItem's checks, the launch query and, later, the engine's and the item view's launch facts read launches through it.</summary>
public interface ILaunchViews
{
    /// <summary>One launch.</summary>
    /// <param name="launch">The launch.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The row, or null when there is none.</returns>
    public ValueTask<Result<LaunchRow?>> Get(LaunchId launch, CancellationToken cancellationToken);

    /// <summary>An item's launches, newest request first.</summary>
    /// <param name="item">The item.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The rows.</returns>
    public ValueTask<Result<IReadOnlyList<LaunchRow>>> ForItem(ItemId item, CancellationToken cancellationToken);

    /// <summary>A repo's launches, newest request first.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The rows.</returns>
    public ValueTask<Result<IReadOnlyList<LaunchRow>>> ForRepo(RepoName repo, CancellationToken cancellationToken);
}
