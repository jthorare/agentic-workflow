using System.Diagnostics;
using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Infrastructure.Processes;
using Aw.Meta.Testing;

namespace Aw.Meta.Tests.Ports;

/// <summary>The macOS process table against the real ps, sysctl and vm_stat; skipped everywhere else.</summary>
[Lane(TestLane.Integration)]
public sealed class MacProcessTableContractTests : ProcessTableContract, IDisposable
{
    private readonly List<Process> _started = [];

    protected override IProcessTable Create()
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "the macOS adapter runs on macOS");
        return new MacProcessTable(new MacHostReader(Path.GetTempPath(), new SystemCommandRunner(), MacToolPaths.Default, DiskSpace.Free));
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
