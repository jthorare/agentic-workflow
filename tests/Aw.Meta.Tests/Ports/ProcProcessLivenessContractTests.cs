using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Ports;

/// <summary>The Proc process liveness against real processes; skipped on the other OS.</summary>
[Lane(TestLane.Integration)]
public sealed class ProcProcessLivenessContractTests : ProcessLivenessContract, IDisposable
{
    private readonly RealProcesses _processes = new();

    protected override IProcessLiveness Create()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "this adapter runs on Linux");
        return RealLiveness.Create();
    }

    protected override int StartProcess() => _processes.Start();

    protected override void StopProcess(int pid) => _processes.Stop(pid);

    public void Dispose() => _processes.Dispose();
}
