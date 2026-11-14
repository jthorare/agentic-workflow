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

        var approved = await approvals.ForRead(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var agents = approved.Value?.Profile.Agents;
        if (latest.Value is not { } recorded)
        {
            return LaunchTargetsProjection.Project(probed: null, probedAt: null, agents, TodaysSpend.None, asOf);
        }

        var probed = events.Payload<LaunchProbed>(recorded);
        if (probed.IsError)
        {
            return probed.Error;
        }

        return LaunchTargetsProjection.Project(probed.Value, recorded.Envelope.OccurredAt, agents, Spent(probed.Value), asOf);
    }

    /// <summary>What each agent has spent today as far as it is known now: an agent whose first read of the day hasn't landed is counting, and nothing here waits (#3418).</summary>
    private TodaysSpend Spent(LaunchProbed probed)
    {
        var used = new Dictionary<string, AgentSpend>(StringComparer.Ordinal);
        var counting = new HashSet<string>(StringComparer.Ordinal);
        foreach (var target in probed.Targets)
        {
            var peek = spend.Peek(target.Id);
            if (!peek.Landed)
            {
                _ = counting.Add(target.Id.Value);
            }
            else if (peek.Spend is { } found)
            {
                used[target.Id.Value] = found;
            }
        }

        return new TodaysSpend(used, counting);
    }
}
