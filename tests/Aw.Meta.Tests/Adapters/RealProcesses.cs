using System.Diagnostics;

namespace Aw.Meta.Tests.Adapters;

/// <summary>Starts and ends real sleep processes for the integration tests, and reaps them on dispose.</summary>
internal sealed class RealProcesses : IDisposable
{
    private readonly List<Process> _started = [];

    internal int Start()
    {
        var info = new ProcessStartInfo("sleep") { UseShellExecute = false };
        info.ArgumentList.Add("120");
        var process = Process.Start(info) ?? throw new InvalidOperationException("sleep did not start");
        _started.Add(process);
        return process.Id;
    }

    internal void Stop(int pid)
    {
        var process = _started.Single(p => p.Id == pid);
        if (!process.HasExited)
        {
            process.Kill();
        }

        process.WaitForExit();
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
