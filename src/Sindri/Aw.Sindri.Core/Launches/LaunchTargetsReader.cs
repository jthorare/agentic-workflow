using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Launches;

/// <summary>The last recorded probe with today's budgets (API spec 'GET /launch/targets'): shared by the targets route and an item's launch block.</summary>
/// <param name="ledger">Reads the last 'launch.probed'.</param>
/// <param name="events">Reads its payload.</param>
/// <param name="approvals">The approved profile, which names the budgets.</param>
/// <param name="spend">What each agent spent today.</param>
/// <param name="status">How fresh the views are.</param>
public sealed class LaunchTargetsReader(ILedgerReader ledger, EventFactory events, IApprovedProfile approvals, ILaunchSpend spend, IProjectorStatus status)
{
    /// <summary>Reads the last probe and prices today's spend against the budgets.</summary>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The projection, or why a read failed.</returns>
    public async ValueTask<Result<LaunchTargets>> Read(CancellationToken cancellationToken)
    {
        var asOf = status.Current.AsOf;
        var latest = await ledger.Latest(EventTypes.LaunchProbed, cancellationToken).ConfigureAwait(false);
        if (latest.IsError)
        {
            return latest.Error;
        }

        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var agents = approved.Value?.Profile.Agents;
        if (latest.Value is not { } recorded)
        {
            return LaunchTargetsProjection.Project(probed: null, probedAt: null, agents, new Dictionary<string, AgentSpend>(StringComparer.Ordinal), asOf);
        }

        var probed = events.Payload<LaunchProbed>(recorded);
        if (probed.IsError)
        {
            return probed.Error;
        }

        var used = await Spent(probed.Value, cancellationToken).ConfigureAwait(false);
        return used.IsError ? used.Error : LaunchTargetsProjection.Project(probed.Value, recorded.Envelope.OccurredAt, agents, used.Value, asOf);
    }

    private async ValueTask<Result<IReadOnlyDictionary<string, AgentSpend>>> Spent(LaunchProbed probed, CancellationToken cancellationToken)
    {
        var used = new Dictionary<string, AgentSpend>(StringComparer.Ordinal);
        foreach (var target in probed.Targets)
        {
            var today = await spend.Today(target.Id, cancellationToken).ConfigureAwait(false);
            if (today.IsError)
            {
                return today.Error;
            }

            if (today.Value is { } found)
            {
                used[target.Id.Value] = found;
            }
        }

        return used;
    }
}
