using System.Diagnostics;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Lease;
using Aw.Sindri.Infrastructure.Lease;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Lease;

/// <summary>The POSIX probe: a real child that exited is the gone pid; this process with an hour-earlier start stands for a reused pid.</summary>
[Lane(TestLane.Integration)]
public sealed class PosixProcessProbeContractTests : ProcessProbeContract
{
    private readonly PosixProcessProbe _probe = new();

    protected override IProcessProbe Create() => _probe;

    protected override async ValueTask<int> GonePid()
    {
        using var child = Process.Start(new ProcessStartInfo("/bin/sh", ["-c", "exit 0"]) { UseShellExecute = false })!;   // why: Start returns a process for a file that exists
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        await child.WaitForExitAsync(deadline.Token);
        return child.Id;
    }

    protected override (int Pid, DateTimeOffset EarlierStart) ReusedPid() => (Environment.ProcessId, _probe.Self().PidStart.AddHours(-1));
}
