using Aw.Kernel.Testing;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Lease;

[Lane(TestLane.Unit)]
public sealed class FakeProcessProbeContractTests : ProcessProbeContract
{
    private readonly FakeProcessProbe _probe = new();

    protected override IProcessProbe Create() => _probe;

    protected override ValueTask<int> GonePid() => ValueTask.FromResult(999);

    protected override (int Pid, DateTimeOffset EarlierStart) ReusedPid()
    {
        _probe.Start(FakeProcessProbe.Holder(300, KnownInstants.Epoch.AddMinutes(5)));
        return (300, KnownInstants.Epoch);
    }
}
