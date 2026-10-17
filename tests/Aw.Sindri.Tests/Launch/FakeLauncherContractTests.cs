using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Testing.Launch;

namespace Aw.Sindri.Tests.Launch;

[Lane(TestLane.Unit)]
public sealed class FakeLauncherContractTests : LauncherContract
{
    private readonly FakeLauncher _launcher = new();

    protected override ILauncher Create() => _launcher;

    protected override int SessionCount() => _launcher.SessionCount;
}
