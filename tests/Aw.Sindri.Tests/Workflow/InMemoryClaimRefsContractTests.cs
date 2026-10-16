using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Effects;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

[Lane(TestLane.Unit)]
public sealed class InMemoryClaimRefsContractTests : ClaimRefsContract
{
    protected override ValueTask<(IClaimRefs Refs, string Repository)> Create() =>
        ValueTask.FromResult<(IClaimRefs, string)>((new InMemoryClaimRefs(), "/repos/dev-repo"));
}
