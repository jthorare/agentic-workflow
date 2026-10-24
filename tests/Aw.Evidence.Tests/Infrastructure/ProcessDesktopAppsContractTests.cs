using Aw.Evidence.Infrastructure.Desktop;
using Aw.Evidence.Testing;
using Aw.Kernel.Testing;

namespace Aw.Evidence.Tests.Infrastructure;

/// <summary>The real starter over a stub program that sleeps; the launch sets DISPLAY, so no virtual X server is needed.</summary>
[Lane(TestLane.Integration)]
public sealed class ProcessDesktopAppsContractTests : DesktopAppsContract, IDisposable
{
    private readonly TempDirectory _root = new();

    protected override Task<DesktopAppsUnderTest> ArrangeAsync()
    {
        _ = Directory.CreateDirectory(_root.Combine("scripts"));
        _ = StubCommand.Write(_root, "scripts/app.sh", "exec sleep 30");
        return Task.FromResult(new DesktopAppsUnderTest(new ProcessDesktopApps(), "scripts/app.sh", _root.Path));
    }

    protected override Task<DesktopAppsUnderTest> ArrangeMissingAsync() =>
        Task.FromResult(new DesktopAppsUnderTest(new ProcessDesktopApps(), "scripts/absent.sh", _root.Path));

    public void Dispose() => _root.Dispose();
}
