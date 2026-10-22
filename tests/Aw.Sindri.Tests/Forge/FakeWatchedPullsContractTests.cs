using Aw.Kernel.Testing;
using Aw.Sindri.Testing.Forge;

namespace Aw.Sindri.Tests.Forge;

[Lane(TestLane.Unit)]
public sealed class FakeWatchedPullsContractTests : WatchedPullsContract
{
    protected override WatchedPullsUnderTest Create()
    {
        var pulls = new FakeWatchedPulls();
        return new(pulls, pulls.Watch, pulls.Unwatch);
    }
}
