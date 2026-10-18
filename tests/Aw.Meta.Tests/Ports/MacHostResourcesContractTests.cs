using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Infrastructure.Processes;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Ports;

/// <summary>The macOS host resources against the real sysctl and vm_stat; skipped everywhere else.</summary>
[Lane(TestLane.Integration)]
public sealed class MacHostResourcesContractTests : HostResourcesContract, IDisposable
{
    private readonly TempDirectory _state = new();
    private int _slots;

    private string Lock => Path.Combine(_state.Path, "locks", "heavy-job.lock");

    protected override IHostResources Create()
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "the macOS adapter runs on macOS");
        return new MacHostResources(new MacHostReader(_state.Path, new SystemCommandRunner(), MacToolPaths.Default, DiskSpace.Free), Lock, RealLiveness.Create());
    }

    protected override void TakeLock(IHostResources resources, int pid)
    {
        var slot = ++_slots == 1 ? Lock : string.Create(CultureInfo.InvariantCulture, $"{Lock}.{_slots}");
        _ = Directory.CreateDirectory(slot);
        File.WriteAllText(Path.Combine(slot, "owner"), string.Create(CultureInfo.InvariantCulture, $"{pid} 2026-10-15T04:00:00Z session=1 dotnet build"));
    }

    public void Dispose() => _state.Dispose();
}
