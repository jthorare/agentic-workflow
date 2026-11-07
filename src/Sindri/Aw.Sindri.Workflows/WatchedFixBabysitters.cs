using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// <see cref="IFixBabysitters"/> from the pull requests the live babysitter instances wait on (<see cref="IWatchedPulls"/>, the PR watcher's list): an instance names its pull request by its
/// 'fact:pr' link, so a pull request is watched when one of those links is its link (compared without case and without a trailing slash).
/// </summary>
/// <param name="watched">The pull requests of the live instances.</param>
internal sealed class WatchedFixBabysitters(IWatchedPulls watched) : IFixBabysitters
{
    public async ValueTask<bool> Watching(string link, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(link);
        var listed = await watched.List(cancellationToken).ConfigureAwait(false);
        return listed.IsSuccess && listed.Value.Any(w => string.Equals(w.Link.TrimEnd('/'), link.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
    }
}
