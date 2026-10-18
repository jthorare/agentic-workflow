using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Integration)]
public sealed class ProcHostResourcesContractTests : HostResourcesContract, IDisposable
{
    private readonly TempDirectory _state = new();
    private int _slots;

    private string Lock => Path.Combine(_state.Path, "locks", "heavy-job.lock");

    protected override IHostResources Create()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the /proc adapter reads /proc");
        return new ProcHostResources(_state.Path, Lock, RealLiveness.Create());
    }

    protected override void TakeLock(IHostResources resources, int pid)
    {
        var slot = ++_slots == 1 ? Lock : string.Create(CultureInfo.InvariantCulture, $"{Lock}.{_slots}");
        _ = Directory.CreateDirectory(slot);
        File.WriteAllText(Path.Combine(slot, "owner"), string.Create(CultureInfo.InvariantCulture, $"{pid} 2026-10-15T04:00:00Z session=1 dotnet build"));
    }

    public void Dispose() => _state.Dispose();
}
