using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph.Runs;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Ledger;

[Lane(TestLane.Unit)]
public sealed class InMemoryWorkflowRunStoreContractTests : WorkflowRunStoreContract
{
    protected override IWorkflowRunStore Create(TimeProvider clock) => new InMemoryWorkflowRunStore(clock);
}
