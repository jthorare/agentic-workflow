using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Control;

[Lane(TestLane.Unit)]
public sealed class InMemoryEndpointFileContractTests : EndpointFileContract
{
    protected override IEndpointFile Create() => new InMemoryEndpointFile();

    protected override void Damage(IEndpointFile file) => ((InMemoryEndpointFile)file).Damage();
}
