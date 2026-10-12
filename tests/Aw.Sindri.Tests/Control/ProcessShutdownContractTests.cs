using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Host.Server;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Control;

[Lane(TestLane.Unit)]
public sealed class ProcessShutdownContractTests : ShutdownContract
{
    protected override IShutdown Create() => new ProcessShutdown();
}
