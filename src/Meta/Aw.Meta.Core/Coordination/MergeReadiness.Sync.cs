using Aw.Kernel;
using Aw.Meta.Core.Merge;

namespace Aw.Meta.Core.Coordination;

// The one entry merge-check and merge-run share: it syncs the meta's own transcript before the checks read the messages.
public sealed partial class MergeReadiness
{
    /// <summary>Gets a value indicating whether the check syncs the meta's own transcript first (it was given an <see cref="InboxSync"/>).</summary>
    internal bool Syncs => _sync is not null;

    /// <summary>Checks a pull request.</summary>
    /// <param name="pr">The pull request number.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The checks, or an error from the store, git or gh.</returns>
    public async ValueTask<Result<MergeAssessment>> Assess(int pr, CancellationToken cancellationToken)
    {
        // Only the meta's Stop hook syncs its inbox, so an OK, ready or HOLD line from the meta's current turn sits in its
        // transcript and not in the store yet (#4332): read the transcript first, as lane-recycle does. A failed sync is
        // advisory: the checks run on what is stored and a passing "inbox-sync" row says the sync failed.
        var synced = _sync is null ? Results.Success(value: false) : await _sync.RunMeta(cancellationToken).ConfigureAwait(false);
        var assessed = await AssessStored(pr, cancellationToken).ConfigureAwait(false);
        if (assessed.IsError || !synced.IsError)
        {
            return assessed;
        }

        var note = new MergeCheck("inbox-sync", Ok: true, "the meta's transcript did not sync (" + synced.Error.Message + "); the checks ran on the stored messages");
        return assessed.Value with { Checks = [.. assessed.Value.Checks, note] };
    }
}
