using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Control;

[Lane(TestLane.Unit)]
public sealed class FakeShutdownContractTests : ShutdownContract
{
    protected override IShutdown Create() => new FakeShutdown();
}
