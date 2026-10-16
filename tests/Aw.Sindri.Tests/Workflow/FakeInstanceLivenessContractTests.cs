using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

[Lane(TestLane.Unit)]
public sealed class FakeInstanceLivenessContractTests : InstanceLivenessContract
{
    private readonly FakeInstanceLiveness _fake = new();

    protected override ValueTask<IInstanceLiveness> Create() => ValueTask.FromResult<IInstanceLiveness>(_fake);

    protected override Task Run()
    {
        _ = _fake.Live.Add(Address);
        return Task.CompletedTask;
    }

    protected override Task End()
    {
        _ = _fake.Live.Remove(Address);
        return Task.CompletedTask;
    }
}
