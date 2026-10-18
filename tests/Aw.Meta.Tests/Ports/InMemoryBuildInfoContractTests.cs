using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Unit)]
public sealed class InMemoryBuildInfoContractTests : BuildInfoContract
{
    protected override IBuildInfo Create() => new InMemoryBuildInfo();
}
