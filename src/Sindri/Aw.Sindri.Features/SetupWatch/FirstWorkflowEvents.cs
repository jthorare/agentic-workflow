using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.SetupWatch;

/// <summary>
/// The ledger side of hello-sindri (first-run spec decision 19, E2): 'setup.first_workflow.created' says the setup service made it once. The key has no suffix, so there is one per box, and
/// a hello-sindri the owner deleted or dismissed is never made again.
/// </summary>
/// <param name="held">The lease the event is fenced by.</param>
/// <param name="ledger">Appends the event.</param>
/// <param name="reader">Looks for an earlier one.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="callers">The caller whose command id the event carries.</param>
internal sealed class FirstWorkflowEvents(HeldLease held, ILedgerStore ledger, ILedgerReader reader, EventFactory events, ICallerContextAccessor callers)
{
    /// <summary>Whether the ledger holds the event.</summary>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>True when it does; false when it doesn't; null when the ledger could not be read, so nothing is made on a guess.</returns>
    internal async ValueTask<bool?> WasMade(CancellationToken cancellationToken)
    {
        var latest = await reader.Latest(EventTypes.SetupFirstWorkflowCreated, cancellationToken).ConfigureAwait(false);
        return latest.IsError ? null : latest.Value is not null;
    }

    /// <summary>Appends the event; recording it again is a no-op.</summary>
    /// <param name="slug">The workflow made.</param>
    /// <param name="repo">The repo its item is made in.</param>
    /// <param name="cancellationToken">Cancels the append.</param>
    /// <returns>Success, or SND-API-001 when this process holds no lease or no call is running, or the ledger's error.</returns>
    internal async ValueTask<Result<Unit>> Made(WorkflowSlug slug, RepoName repo, CancellationToken cancellationToken)
    {
        if (held.Current is not { } grant || callers.Current is not { } caller)
        {
            return SindriErrors.NotRunning.With("the control plane doesn't hold the lease (fix: sindri)");
        }

        var appended = await ledger.Append(events.Entry(EventTypes.SetupFirstWorkflowCreated, new SetupFirstWorkflowCreated(caller.CommandId, slug, repo), EventTypes.SetupFirstWorkflowCreated), grant, cancellationToken).ConfigureAwait(false);
        return appended.IsError ? appended.Error : Unit.Value;
    }
}
