using System.Diagnostics;
using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Integration)]
public sealed class ProcProcessTableContractTests : ProcessTableContract, IDisposable
{
    private readonly List<Process> _started = [];

    protected override IProcessTable Create()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "the /proc adapter reads /proc");
        return new ProcProcessTable(Path.GetTempPath());
    }

    protected override int StartProcess()
    {
        var info = new ProcessStartInfo("sleep") { UseShellExecute = false };
        info.ArgumentList.Add("120");
        var process = Process.Start(info) ?? throw new InvalidOperationException("sleep did not start");
        _started.Add(process);
        return process.Id;
    }

    public void Dispose()
    {
        foreach (var process in _started)
        {
            if (!process.HasExited)
            {
                process.Kill();
            }

            process.WaitForExit();
            process.Dispose();
        }
    }
}
