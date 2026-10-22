using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Testing.Forge;

namespace Aw.Sindri.Tests.Forge;

[Lane(TestLane.Unit)]
public sealed class FakePullRequestsContractTests : PullRequestsContract
{
    protected override PullRequestsUnderTest Create()
    {
        var pulls = new FakePullRequests(["forge.test"]);
        return new(pulls, new PullRepo("forge.test", "acme", "widgets"), "elsewhere.test", pulls.SeedThread, CanPush: true);
    }
}
