using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// Which launcher agent runs a contrast Step (#1008): the second opinion runs on an agent other than the one that ran the Step it checks (the drafter), picked from the
/// catalog entries an unattended Step may use. Runs in the launch activity, so a replay reads the pick from history; a launch already recorded keeps its agent, so a retry
/// after the catalog or the approvals changed does not move the Step. "Usable" is the part of the launcher's approval (SL8) readable here: a built-in entry, or an entry
/// whose argv hash the approved profile lists under agents.&lt;id&gt;.stepArgvHash; the launcher still applies its full check when it starts the session. "Different" is a different
/// catalog entry id: two entries that share one CLI count as different agents.
/// </summary>
/// <param name="catalog">The agent catalog.</param>
/// <param name="launches">The launches view, for the drafter's agent and a launch already recorded.</param>
/// <param name="approved">The approved profile, for the argv hashes approved for Steps.</param>
internal sealed class ChallengerPick(IAgentCatalog catalog, ILaunchViews launches, IApprovedProfile approved)
{
    private static readonly BlockId _challenge = BlockId.From("step.scope.challenge");
    private static readonly BlockId _draft = BlockId.From("step.scope.draft");

    /// <summary>The Step whose agent <paramref name="block"/> must differ from.</summary>
    /// <param name="block">The Step a run launches.</param>
    /// <returns>The scoping draft for the scoping challenge; null for any other Step, which runs on the profile's agent.</returns>
    public static BlockId? DrafterBlock(BlockId block) => block == _challenge ? _draft : null;

    /// <summary>The agent the contrast Step runs on.</summary>
    /// <param name="ask">The Step's launch and the Step it differs from.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The agent: the one already recorded for the launch, else the profile default when no drafter launch is known, else a usable one other than the drafter's; SND-LAUNCH-028 when none.</returns>
    public async ValueTask<Result<AgentId>> Pick(ChallengerAsk ask, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ask);
        var recorded = await launches.Get(ask.Launch, cancellationToken).ConfigureAwait(false);
        if (recorded.IsError)
        {
            return recorded.Error;
        }

        if (recorded.Value is { } kept)
        {
            return kept.Target;
        }

        var rows = await launches.ForItem(ask.Item, cancellationToken).ConfigureAwait(false);
        if (rows.IsError)
        {
            return rows.Error;
        }

        // why: the view lists the newest request first, so the first row of the drafter's state is its latest run
        var drafter = rows.Value.FirstOrDefault(r => r.Step.Instance is { } instance && instance.Equals(ask.Instance) && string.Equals(r.Step.State, ask.DrafterState, StringComparison.Ordinal));
        if (drafter is null)
        {
            return ask.Default;   // the drafter ran at the desk or not yet: there is no agent to differ from
        }

        var latest = await approved.Latest(cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        var others = catalog.All.Where(e => e.Id != drafter.Target && Usable(e, latest.Value?.Profile.Agents)).Select(static e => e.Id).ToList();
        if (others.Count == 0)
        {
            return SindriErrors.LaunchNoSecondAgent.With(
                $"the second opinion needs an agent other than {drafter.Target.Value}, and no other agent is usable by Steps (fix: run `sindri launch show`, put another entry's argvHash under agents.<id>.stepArgvHash and approve the profile)");
        }

        return others.Contains(ask.Default) ? ask.Default : others[0];
    }

    private static bool Usable(AgentEntry entry, AgentSettings? settings) =>
        entry.Source == CatalogSource.Builtin || (entry.ArgvHash is { } hash && settings?.StepArgvHashes(entry.Id).Contains(hash, StringComparer.Ordinal) == true);
}
