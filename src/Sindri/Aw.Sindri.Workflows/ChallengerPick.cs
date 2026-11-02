using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Which launcher agent runs a contrast Step (#1008): the second opinion runs on an agent other than the one that ran the Step it checks (the drafter), picked from the
/// catalog entries an unattended Step may use, and carries the drafter's launch to the model pick. With no second usable agent it stays on the profile's agent and the
/// model pick takes the drafter's model out of the candidates (model-pick spec 4.1).
/// </summary>
/// <param name="catalog">The agent catalog.</param>
/// <param name="launches">The launches view, for the drafter's agent and model.</param>
/// <param name="approved">The approved profile, for the argv hashes approved for Steps.</param>
internal sealed class ChallengerPick(IAgentCatalog catalog, ILaunchViews launches, IApprovedProfile approved)
{
    /// <summary>The agent the contrast Step runs on, and the drafter it differs from.</summary>
    /// <param name="ask">The Step's launch and the Step it differs from.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The choice; SND-LAUNCH-028 when no agent in the catalog may run a Step.</returns>
    public async ValueTask<Result<ChallengerChoice>> Pick(ChallengerAsk ask, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ask);
        var rows = await launches.ForItem(ask.Item, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        var usable = await Usable(cancellationToken).ConfigureAwait(false);
        if (usable.IsError)
        {
            return usable.Error;
        }

        var drafter = rows.Value.FirstOrDefault(r => r.Step.Instance == ask.Instance && string.Equals(r.Step.State, ask.DrafterState, StringComparison.Ordinal) && r.State == LaunchState.Started);
        var agent = rows.Value.FirstOrDefault(r => r.Id == ask.Launch)?.Target ?? Choose(ask.Default, drafter?.Target, usable.Value);
        return agent is { } chosen
            ? new ChallengerChoice(chosen, drafter is null ? null : new DrafterModel(chosen.Equals(drafter.Target), drafter.Model))
            : SindriErrors.LaunchNoUsableAgent.With("no agent in the catalog may run a Step, so the second opinion has nowhere to run (fix: run `sindri launch show`, put an agent's argvHash under agents.<id>.stepArgvHash, then approve the profile)");
    }

    // The catalog's agents an unattended Step may use (SL8), in catalog order.
    private async ValueTask<Result<List<AgentId>>> Usable(CancellationToken cancellationToken)
    {
        var limits = await approved.Latest(cancellationToken).ConfigureAwait(false);
        return limits.IsError
            ? limits.Error
            : catalog.All.Where(e => StepApproval.Check(e, builtinArgvHash: null, limits.Value?.Profile.Agents?.StepArgvHashes(e.Id) ?? []).IsSuccess).Select(static e => e.Id).ToList();
    }

    // The profile's agent when it differs from the drafter's, else the first usable agent that does, else the profile's agent (the same one; the model pick then differs by model).
    private static AgentId? Choose(AgentId preferred, AgentId? drafter, List<AgentId> usable)
    {
        if (drafter is not { } ran)
        {
            return preferred;
        }

        if (!preferred.Equals(ran) && usable.Contains(preferred))
        {
            return preferred;
        }

        var other = usable.Where(id => !id.Equals(ran)).Cast<AgentId?>().FirstOrDefault();
        return other ?? (usable.Contains(preferred) ? preferred : usable.Count > 0 ? usable[0] : null);
    }
}
