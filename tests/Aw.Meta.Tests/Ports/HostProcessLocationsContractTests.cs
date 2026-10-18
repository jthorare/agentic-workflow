using System.Diagnostics;
using Aw.Kernel.Testing;
using Aw.Meta.Core.Ports;
using Aw.Meta.Infrastructure.Files;
using Aw.Meta.Infrastructure.Processes;
using Aw.Meta.Testing;
using Aw.Meta.Tests.Adapters;

namespace Aw.Meta.Tests.Ports;

[Lane(TestLane.Integration)]
public sealed class HostProcessLocationsContractTests : ProcessLocationsContract, IDisposable
{
    private readonly List<Process> _started = [];
    private readonly List<string> _directories = [];

    protected override IProcessLocations Create() =>
        OperatingSystem.IsMacOS()
            ? new MacProcessLocations(new MacHostReader(Path.GetTempPath(), new SystemCommandRunner(), MacToolPaths.Default, DiskSpace.Free), Environment.ProcessId)
            : new ProcProcessLocations();

    [Fact]
    public async Task The_scanning_process_is_listed_as_an_invoker()
    {
        var scan = await Create().Locations(TestContext.Current.CancellationToken);

        Assert.Contains(scan.Value, p => p.Pid == Environment.ProcessId && p.IsInvoker);
    }

    protected override int StartProcessIn(string directory)
    {
        var info = new ProcessStartInfo("sleep") { UseShellExecute = false, WorkingDirectory = directory };
        info.ArgumentList.Add("120");
        var process = Process.Start(info) ?? throw new InvalidOperationException("sleep did not start");
        _started.Add(process);
        return process.Id;
    }

    protected override string NewDirectory()
    {
        var path = RealPath.Of(Directory.CreateTempSubdirectory("aw-meta-scan-").FullName);
        _directories.Add(path);
        return Path.GetFullPath(path);
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

        foreach (var directory in _directories)
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
