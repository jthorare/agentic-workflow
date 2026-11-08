using Aw.Kernel.Testing;
using Aw.Sindri.Core.Graph;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Ledger;

[Lane(TestLane.Unit)]
public sealed class InMemoryWorkflowStoreContractTests : WorkflowStoreContract
{
    protected override IWorkflowStore Create() => new InMemoryWorkflowStore(new FakeTimeProvider(KnownInstants.Epoch));
}
