using System.Text;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Features.GetInstance;

/// <summary>
/// The two read-time joins of the instance detail that the engine's view can't make (q65): the agent session of the open run, from its launch row, and where each reviewed
/// fact opens, from the fact's stored value. Neither is held by the workflow, so a replay never depends on them.
/// </summary>
/// <param name="launches">The launches view.</param>
/// <param name="contents">The stored fact content.</param>
/// <param name="ledger">The ledger, for a launch's wait for a free session slot; null serves none.</param>
/// <param name="events">Reads a payload; null serves none.</param>
internal sealed class InstanceJoins(ILaunchViews launches, IFactContents contents, ILedgerReader? ledger = null, EventFactory? events = null)
{
    /// <summary>Whether the open agent run's launch waits for a free session slot: 'launch.waiting' is on the ledger and 'launch.outcome' is not yet.</summary>
    /// <param name="view">The instance's view.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>How many sessions run and the cap; null when the run does not wait, has no agent run, or the ledger can't be read.</returns>
    public async ValueTask<StatusWaiting?> Waiting(InstanceView view, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (ledger is null || events is null || view.AgentRun?.Launch is not { } launch)
        {
            return null;
        }

        var outcome = await ledger.ByKey("launch.outcome:" + launch, cancellationToken).ConfigureAwait(false);
        if (outcome is not { IsSuccess: true, Value: null })
        {
            return null;   // the launch has an outcome, or the ledger can't say
        }

        var waited = await ledger.ByKey("launch.waiting:" + launch, cancellationToken).ConfigureAwait(false);
        var payload = waited is { IsSuccess: true, Value: { } row } ? events.Payload<LaunchWaiting>(row) : default;
        return payload is { IsSuccess: true } ? new StatusWaiting(payload.Value.Running, payload.Value.Cap) : null;
    }

    /// <summary>The session of <paramref name="view"/>'s open agent run.</summary>
    /// <param name="view">The instance's view.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The launch's agent and session id (the one known at start, else the reported one), the handle the Agents page addresses it by; null for a desk run, a launch that knows no id yet, a missing launch or one that can't be read.</returns>
    public async ValueTask<SessionHandle?> Session(InstanceView view, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(view);
        if (view.AgentRun?.Launch is not { } launch || !LaunchId.TryFrom(launch, out var id))
        {
            return null;
        }

        var row = await launches.Get(id, cancellationToken).ConfigureAwait(false);
        return row is { IsSuccess: true, Value: { ResumableId: { } session } found } ? new SessionHandle(found.Target, session) : null;
    }

    /// <summary>Where each fact the open desk reviews opens.</summary>
    /// <param name="view">The instance's view.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The targets by fact key; empty when the desk reviews nothing, and no entry for a fact that is not held or can't be read.</returns>
    public async ValueTask<IReadOnlyDictionary<string, DeskReviewTarget>> ReviewTargets(InstanceView view, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(view);
        var targets = new Dictionary<string, DeskReviewTarget>(StringComparer.Ordinal);
        foreach (var review in view.Desk?.Reviews ?? [])
        {
            if (view.FactShas?.GetValueOrDefault(review.Fact) is not { } sha)
            {
                continue;
            }

            var bytes = await contents.Read(new FactValue("fact:sha256:" + sha, sha, HeadSha: null), cancellationToken).ConfigureAwait(false);
            if (bytes.IsSuccess)
            {
                targets[review.Fact] = DeskReviewTargets.Of(Encoding.UTF8.GetString(bytes.Value.Span));
            }
        }

        return targets;
    }
}
