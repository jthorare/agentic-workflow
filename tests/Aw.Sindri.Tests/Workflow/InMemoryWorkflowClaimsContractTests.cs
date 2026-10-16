using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

[Lane(TestLane.Unit)]
public sealed class InMemoryWorkflowClaimsContractTests : WorkflowClaimsContract
{
    protected override IWorkflowClaims Create() => new InMemoryWorkflowClaims();
}
