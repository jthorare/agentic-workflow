using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetLaunchTargets;

/// <summary>Serves the newest 'launch.probed' with each target's daily budget and today's spend (UI spec E-3, screens spec E-28).</summary>
/// <param name="ledger">The ledger, for the newest probe.</param>
/// <param name="events">Reads the payload.</param>
/// <param name="approvals">The approved profile: budgets.</param>
/// <param name="spend">What each agent has spent today.</param>
/// <param name="status">How fresh the views are.</param>
internal sealed class GetLaunchTargetsService(ILedgerReader ledger, EventFactory events, IApprovedProfile approvals, ILaunchSpend spend, IProjectorStatus status)
    : IService<GetLaunchTargetsRequest, LaunchTargets>
{
    public async ValueTask<Result<LaunchTargets>> Handle(GetLaunchTargetsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
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
