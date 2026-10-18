using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.Launching;

/// <summary>Turns a request into the launcher's input (UI spec E-1): the attended launch of the request's agent in the item's repo checkout.</summary>
/// <param name="reader">Reads the item.</param>
/// <param name="profile">The approved profile, which names each repo's path and each agent's daily budget.</param>
/// <param name="spend">What each agent has spent today, for the budget re-check (screens spec E-28).</param>
/// <param name="launches">The 'launches' view, for the session a resume addresses.</param>
internal sealed class LaunchPlans(IViewReader reader, IApprovedProfile profile, ILaunchSpend spend, ILaunchViews launches)
{
    private static string? RepoPath(RepoName? repo, IReadOnlyList<RepoEntry>? repos) =>
        repo is { } named ? repos?.FirstOrDefault(r => string.Equals(r.Name, named.Value, StringComparison.Ordinal))?.Path : null;

    private async ValueTask<Error?> Budget(AgentId target, AgentSettings? agents, CancellationToken cancellationToken)
    {
        var spent = await spend.Today(target, cancellationToken).ConfigureAwait(false);
        return spent.IsError ? spent.Error : LaunchBudgets.Refusal(LaunchBudgets.Of(agents, target, spent.Value), target);
    }

    /// <summary>Builds the launch.</summary>
    /// <param name="pending">The request.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The launch, or SND-LAUNCH-004 when the item's repo has no path in the approved profile.</returns>
    public async ValueTask<Result<LaunchRequest>> Build(PendingLaunch pending, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pending);
        var request = pending.Request;
        var item = await reader.Get<ItemRow>(ViewNames.Items, request.Item.Value, cancellationToken).ConfigureAwait(false);
        if (item.IsError)
        {
            return item.Error;
        }

        var approved = await profile.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        if (request.Mode != LaunchMode.Attach && await Budget(request.Target, approved.Value?.Profile.Agents, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;   // the spend moved since the open (E-28): an attach starts no process, so only a start is held to it
        }

        var path = RepoPath(item.Value?.Repo, approved.Value?.Profile.Repos);
        if (path is null)
        {
            return SindriErrors.LaunchRepoMissing.With($"{request.Item.Value} has no repo with a path in the approved profile (fix: add the repo to repos and approve)");
        }

        var resume = request.Mode == LaunchMode.Resume ? await ResumableSession(request, cancellationToken).ConfigureAwait(false) : null;
        return new LaunchRequest(request.Launch, request.Item, request.Target, request.Mode, Attended: true, path, Instance: null, Worktree: null, Step: null, Run: null, resume);
    }

    /// <summary>The session a resume addresses: the newest started launch of the picked agent that has an id (the same rule the open admitted it by).</summary>
    private async ValueTask<SessionId?> ResumableSession(Core.Events.ItemOpenRequested request, CancellationToken cancellationToken)
    {
        var history = await launches.ForItem(request.Item, cancellationToken).ConfigureAwait(false);
        return history.IsError ? null : history.Value.FirstOrDefault(row => row.Target == request.Target && row.State == LaunchState.Started && row.ResumableId is not null)?.ResumableId;
    }
}
