using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

[Lane(TestLane.Unit)]
public sealed class InMemoryLaunchRequestsContractTests : LaunchRequestsContract
{
    protected override ILaunchRequests Create() => new InMemoryLaunchRequests();

    protected override ILaunchRequests? CreateRequesting(ItemKey item, LaunchId launch)
    {
        var launches = new InMemoryLaunchRequests();
        launches.Request(item, launch);
        return launches;
    }
}
