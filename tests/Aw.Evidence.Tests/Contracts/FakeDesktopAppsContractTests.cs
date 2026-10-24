using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Contracts;

[Lane(TestLane.Unit)]
public sealed class FakeDesktopAppsContractTests : DesktopAppsContract
{
    private const string Tool = "scripts/app.sh";

    protected override Task<DesktopAppsUnderTest> ArrangeAsync() => Task.FromResult(new DesktopAppsUnderTest(new FakeDesktopApps(), Tool, "/work/repo"));

    protected override Task<DesktopAppsUnderTest> ArrangeMissingAsync()
    {
        var fake = new FakeDesktopApps();
        _ = fake.WontStart.Add(Tool);
        return Task.FromResult(new DesktopAppsUnderTest(fake, Tool, "/work/repo"));
    }
}
