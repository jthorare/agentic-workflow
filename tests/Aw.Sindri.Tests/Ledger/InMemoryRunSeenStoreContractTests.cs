using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Ledger;

/// <summary>The in-memory run store's seen marks against the seen store contract.</summary>
[Lane(TestLane.Unit)]
public sealed class InMemoryRunSeenStoreContractTests : RunSeenStoreContract
{
    protected override (IWorkflowRunStore Runs, IRunSeenStore Seen) Create(TimeProvider clock)
    {
        var store = new InMemoryWorkflowRunStore(clock);
        return (store, store);
    }
}
