using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Unit)]
public sealed class InMemoryProcessLivenessContractTests : ProcessLivenessContract
{
    private readonly InMemoryProcessLiveness _liveness = new();
    private int _next = 7000;

    protected override IProcessLiveness Create() => _liveness;

    protected override int StartProcess()
    {
        _liveness.Run(++_next, RealTime.NowOffset);
        return _next;
    }

    protected override void StopProcess(int pid) => _liveness.Stop(pid);
}
