using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Control;

[Lane(TestLane.Unit)]
public sealed class FakeControlPlaneProbeContractTests : ControlPlaneProbeContract
{
    protected override ValueTask<IControlPlaneProbe> Create((int Pid, long Epoch)? recorded, Ping answer) =>
        ValueTask.FromResult<IControlPlaneProbe>(FakeControlPlaneProbe.For(
            recorded is { } r ? new ControlEndpoint(new Uri("http://127.0.0.1:7410"), r.Pid, r.Epoch) : null, answer));
}
