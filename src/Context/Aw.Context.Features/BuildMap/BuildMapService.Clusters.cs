using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;

namespace Aw.Context.Features.BuildMap;

/// <summary>The graphify step of the build.</summary>
internal sealed partial class BuildMapService
{
    // A cluster source that fails reads like one that is absent; a denied path never reaches the labeler or the graph.
    // Graphify writes nothing while it runs: a run that bounds itself (timeoutMinutes above 0) says it is alive every 5 min; with no limit it stays silent, so the stall watch can catch a hang.
    private async ValueTask<ClusterSet> Clusters(IProgress<string>? progress, ContextRepo repo, string commit, ClusterLimits limits, CancellationToken cancellationToken)
    {
        var set = limits.TimeoutMinutes > 0
            ? await StepHeartbeat.Run("graphify", progress, clock, () => clusterSource.Clusters(repo, commit, limits, cancellationToken), cancellationToken).ConfigureAwait(false)
            : await clusterSource.Clusters(repo, commit, limits, cancellationToken).ConfigureAwait(false);
        if (set.IsError || !set.Value.Available)
        {
            return new ClusterSet([], Available: false, set.IsError ? null : set.Value.Reason);
        }

        return new ClusterSet(
            [.. set.Value.Clusters.Select(c => c with { Files = [.. c.Files.Where(f => !repo.Denies(f))] }).Where(c => c.Files.Count > 0)],
            Available: true,
            set.Value.Reason);
    }
}
