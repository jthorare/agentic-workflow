using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Unit)]
public sealed class InMemoryCodexRolloutsContractTests : CodexRolloutsContract
{
    protected override (ICodexRollouts Reader, Action<string, string> Put) Create()
    {
        var fake = new InMemoryCodexRollouts();
        return (fake, (session, content) => fake.Put(session, content, A.T0));
    }
}
