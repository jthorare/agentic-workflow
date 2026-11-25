using Aw.Kernel.Testing;
using Aw.Sindri.Core.Toolkits;
using Aw.Sindri.Testing.Toolkits;

namespace Aw.Sindri.Tests.Toolkits;

[Lane(TestLane.Unit)]
public sealed class InMemoryToolkitCallsContractTests : ToolkitCallsContract
{
    protected override IToolkitCalls Create() => new InMemoryToolkitCalls();
}
