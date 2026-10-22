using Aw.Kernel;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Temporalio.Activities;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// 'ReleaseClaim' (engine P7 plan D9, IDEA-033, spec §5.7): when an instance ends, whatever way (done, dropped, cancelled), the
/// engine frees the item's claim, if this instance still owns it: the marker ref 'refs/sindri/{user}/{item}' is deleted
/// from the repo 'fact:item' names and the 'workflow_claims' row is freed. A claim another instance has taken over since is left
/// alone, ref and row. A failure throws, so Temporal retries the activity.
/// </summary>
/// <param name="claims">The 'workflow_claims' table.</param>
/// <param name="refs">Deletes the marker ref.</param>
/// <param name="profile">The approved profile: the user and the repo's path.</param>
/// <param name="workspaces">Removes an ended phase's worktree.</param>
internal sealed class CleanupActivities(IWorkflowClaims claims, IClaimRefs refs, IApprovedProfile profile, PhaseWorkspaceRemoval workspaces)
{
    /// <summary>The activity: <see cref="Release"/> under the activity's cancellation.</summary>
    /// <param name="run">The ended instance and its item.</param>
    /// <returns>True when the claim was freed now.</returns>
    [Activity("ReleaseClaim")]
    public Task<bool> ReleaseClaimAsync(ReleaseRun run) => Release(run, ActivityExecutionContext.Current.CancellationToken);

    /// <summary>The activity: removes the phase worktree an ended 'workflow.phase' instance owns (LN-WF2).</summary>
    /// <param name="workspace">The stored 'fact:workspace'.</param>
    /// <returns>True when the worktree is gone; false when it was left as it is.</returns>
    [Activity("RemovePhaseWorkspace")]
    public async Task<bool> RemovePhaseWorkspaceAsync(FactValue workspace) =>
        await workspaces.Remove(workspace, ActivityExecutionContext.Current.CancellationToken).ConfigureAwait(false);

    /// <summary>Frees the claim <paramref name="run"/>'s instance holds on its item.</summary>
    /// <param name="run">The ended instance and its item.</param>
    /// <param name="cancellationToken">Cancels the cleanup.</param>
    /// <returns>True when the claim was freed now; false when the item had no claim or another instance holds it.</returns>
    public async Task<bool> Release(ReleaseRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var item = ItemKey.From(run.Item);
        var owner = InstanceAddress.From(run.Address);
        var held = Checked(await claims.Find(item, cancellationToken).ConfigureAwait(false));
        if (held is not { } claim || claim.Owner != owner)
        {
            return false;
        }

        if (await Marker(run, cancellationToken).ConfigureAwait(false) is { } marker)
        {
            _ = Checked(await refs.Unmark(marker.Path, $"refs/sindri/{marker.User}/{run.Item}", cancellationToken).ConfigureAwait(false));
        }

        return Checked(await claims.Release(item, owner, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>The user and the repo path the marker was made with; none when the approved profile no longer lists the repo (the row is still freed).</summary>
    private async ValueTask<(string User, string Path)?> Marker(ReleaseRun run, CancellationToken cancellationToken)
    {
        var approved = Checked(await profile.Latest(cancellationToken).ConfigureAwait(false));
        return approved?.Profile.Repos?.FirstOrDefault(r => string.Equals(r.Name, run.Repo, StringComparison.Ordinal)) is { } entry
            ? (approved.Profile.User, entry.Path)
            : null;
    }

    private static T Checked<T>(Result<T> result) =>
        result.IsError ? throw new InvalidOperationException($"{result.Error.Code}: {result.Error.Message}") : result.Value;
}
