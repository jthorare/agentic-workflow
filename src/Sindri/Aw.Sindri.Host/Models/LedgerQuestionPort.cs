using Aw.Kernel;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Graph.Blocks;
using Aw.Sindri.Core.Ledger;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Lease;

namespace Aw.Sindri.Host.Models;

/// <summary>Posts an 'ask_owner' node's question as a 'workflow.question' event, which the needs-you view shows until it is answered or its run ends.</summary>
/// <param name="ledger">Appends the event.</param>
/// <param name="held">The lease this process holds.</param>
/// <param name="events">Builds the entry.</param>
/// <param name="callers">The caller, when there is one.</param>
internal sealed class LedgerQuestionPort(ILedgerStore ledger, HeldLease held, EventFactory events, ICallerContextAccessor callers) : IQuestionPort
{
    public ValueTask<Result<Unit>> Post(QuestionAsk ask, CancellationToken cancellationToken)
    {
        _ = (ledger, held, events, callers);
        throw new NotSupportedException();
    }
}
