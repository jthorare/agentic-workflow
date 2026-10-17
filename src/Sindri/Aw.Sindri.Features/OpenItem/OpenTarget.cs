using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features.OpenItem;

/// <summary>
/// Which agent an open runs and whether its daily budget allows it (UI spec E-1, X15, screens spec E-28). The agent is the click, else the addressed
/// session's, else the first of the repo's last used, the repo's default, the profile's default and Claude Code that this box's catalog has. The click
/// is not checked against the catalog: the launcher answers SND-LAUNCH-003 for it. An attach starts no process, so the budget doesn't gate it.
/// </summary>
/// <param name="approvals">The approved profile: launch defaults and budgets.</param>
/// <param name="launches">The 'launches' view, for the repo's last used agent.</param>
/// <param name="catalog">This box's agent catalog.</param>
/// <param name="spend">What each agent has spent today.</param>
internal sealed class OpenTarget(IApprovedProfile approvals, ILaunchViews launches, IAgentCatalog catalog, ILaunchSpend spend)
{
    private const string BudgetSoft = "budget-soft";

    /// <summary>Decides the agent for <paramref name="request"/>.</summary>
    /// <param name="request">The open.</param>
    /// <param name="repo">The item's repo, or null.</param>
    /// <param name="session">The launch an attach or resume addresses, or null.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The agent, who chose it and any warning; or the refusal.</returns>
    public async ValueTask<Result<OpenDecision>> Decide(OpenItemRequest request, RepoName? repo, LaunchRow? session, CancellationToken cancellationToken)
    {
        var profile = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (profile.IsError)
        {
            return profile.Error;
        }

        var chosen = await Choose(request, repo, session, profile.Value?.Profile.Launch, cancellationToken).ConfigureAwait(false);
        if (chosen.IsError)
        {
            return chosen.Error;
        }

        var (target, chosenBy) = chosen.Value;
        if (request.Mode == LaunchMode.Attach)
        {
            return new OpenDecision(target, chosenBy, []);
        }

        var budget = await Budget(target, profile.Value?.Profile.Agents, cancellationToken).ConfigureAwait(false);
        return budget.IsError ? budget.Error : new OpenDecision(target, chosenBy, budget.Value == LaunchBudgetState.Soft ? [BudgetSoft] : []);
    }

    private async ValueTask<Result<(AgentId Target, string ChosenBy)>> Choose(OpenItemRequest request, RepoName? repo, LaunchRow? session, LaunchSettings? launch, CancellationToken cancellationToken)
    {
        if (request.Target is { } picked)
        {
            return (picked, "click");
        }

        if (session is not null)
        {
            return (session.Target, "default");
        }

        var lastUsed = await LastUsed(repo, cancellationToken).ConfigureAwait(false);
        if (lastUsed.IsError)
        {
            return lastUsed.Error;
        }

        return LaunchTargetChoice.Choose(catalog, launch, repo, lastUsed.Value) is { } chosen
            ? (chosen, "default")
            : SindriErrors.LaunchAgentMissing.With("no agent of the default rules is in the catalog (fix: run `sindri launch show`, or pick one with --agent)");
    }

    private async ValueTask<Result<AgentId?>> LastUsed(RepoName? repo, CancellationToken cancellationToken)
    {
        if (repo is not { } named)
        {
            return (AgentId?)null;
        }

        var recent = await launches.ForRepo(named, cancellationToken).ConfigureAwait(false);
        return recent.IsError ? recent.Error : recent.Value.FirstOrDefault(static row => row.State == LaunchState.Started)?.Target;
    }

    private async ValueTask<Result<LaunchBudgetState>> Budget(AgentId target, AgentSettings? agents, CancellationToken cancellationToken)
    {
        var spent = await spend.Today(target, cancellationToken).ConfigureAwait(false);
        if (spent.IsError)
        {
            return spent.Error;
        }

        var state = LaunchBudgets.Of(agents, target, spent.Value);
        return LaunchBudgets.Refusal(state, target) is { } refused ? refused : state;
    }
}
