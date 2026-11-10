using Aw.Kernel;
using Aw.Kernel.Ids;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Core.Ledger;

namespace Aw.Sindri.Features.CreateItem;

/// <summary>
/// The repair pass of issue 2588: a 'create_item' node that made its item but could not write the run's 'item.linked' row or the run's item only warned. This reads the node outputs of each run
/// and writes what is missing: per item the link row, per run the earliest made item when the run has none.
/// </summary>
/// <param name="workflows">The workflows, to find their runs.</param>
/// <param name="runs">The runs and their node outputs.</param>
/// <param name="ledger">Appends the link rows.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entries.</param>
/// <param name="callers">Enters 'service:workflow', the actor of the live path.</param>
/// <param name="ids">The command id of a link row.</param>
internal sealed class RunItemRepair(IWorkflowStore workflows, IWorkflowRunStore runs, ILedgerStore ledger, HeldLease held, EventFactory events, ICallerContextAccessor callers, IIdGenerator ids)
{
    /// <summary>Repairs every run of every workflow.</summary>
    /// <param name="cancellationToken">Cancels the pass.</param>
    /// <returns>How many writes it made (link rows and run items), or why it stopped.</returns>
    public ValueTask<Result<int>> RepairAll(CancellationToken cancellationToken)
    {
        _ = (workflows, runs, ledger, held, events, callers, ids, cancellationToken);   // stub until the pass is written (#2588)
        throw new NotSupportedException();
    }
}
