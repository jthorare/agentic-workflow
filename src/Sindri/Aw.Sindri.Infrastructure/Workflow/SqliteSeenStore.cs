using Aw.Kernel;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Graph.Ids;
using Aw.Sindri.Infrastructure.Ledger;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>'workflow_seen' in ledger.db (migration 13): the keys an 'only_new' node has passed on. A read never creates or migrates the file.</summary>
/// <param name="database">The ledger file.</param>
/// <param name="clock">The clock the first-seen times come from.</param>
internal sealed class SqliteSeenStore(LedgerDatabase database, TimeProvider clock) : IGraphSeenStore
{
    public ValueTask<Result<IReadOnlySet<string>>> Seen(WorkflowId workflow, IReadOnlyList<string> keys, CancellationToken cancellationToken)
    {
        _ = (database, clock);
        throw new NotSupportedException("SqliteSeenStore is not written yet");
    }

    public ValueTask<Result<Unit>> Record(WorkflowId workflow, IReadOnlyList<string> keys, CancellationToken cancellationToken) =>
        throw new NotSupportedException("SqliteSeenStore is not written yet");
}
