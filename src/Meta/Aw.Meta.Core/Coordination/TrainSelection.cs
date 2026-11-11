using Aw.Kernel;
using Aw.Meta.Core.Gate;
using Aw.Meta.Core.Merge;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Core.Coordination;

/// <summary>Chooses the pull requests of a train: the ones named, else the merge queue in train order.</summary>
/// <param name="config">The team config.</param>
/// <param name="queue">Reads the merge queue.</param>
/// <param name="values">Holds the pull requests merge-run handed to GitHub's merge queue.</param>
public sealed class TrainSelection(IMetaConfig config, MergeQueueReader queue, IKeyValueStore values)
{
    /// <summary>The pull requests to gate.</summary>
    /// <param name="named">The pull requests named, or empty for the merge queue.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The team config and the candidates, or the error that kept either from loading.</returns>
    public async ValueTask<Result<TrainCandidates>> Choose(IReadOnlyList<int> named, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(named);
        var team = await config.Load(cancellationToken).ConfigureAwait(false);
        if (team.IsError || named.Count > 0)
        {
            return team.IsError ? team.Error : new TrainCandidates(team.Value, [.. named.Distinct()]);
        }

        var queued = await queue.Read(cancellationToken).ConfigureAwait(false);
        var handed = await values.Get(KvKeys.MergeQueued, cancellationToken).ConfigureAwait(false);
        if (queued.IsError || handed.IsError)
        {
            return queued.IsError ? queued.Error : handed.Error;
        }

        // A pull request already in GitHub's merge queue is gated there; gating it again here is waste.
        var inGithub = MergeQueued.Parse(handed.Value);
        return new TrainCandidates(team.Value, TrainOrder.Sort([.. queued.Value.Select(q => q.Pr).Where(pr => !inGithub.ContainsKey(pr))], team.Value.MergeOrder));
    }
}
