using System.ComponentModel;
using System.Diagnostics;

namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>ISandboxProcesses over System.Diagnostics.Process: no shell, both streams captured, stdin closed, the whole tree killed on a timeout.</summary>
internal sealed class SandboxProcesses : ISandboxProcesses
{
    /// <inheritdoc />
    public ProcessResult Run(string program, IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        var start = new ProcessStartInfo(program, arguments)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        Process? process;
        try
        {
            process = Process.Start(start);
        }
        catch (Win32Exception)
        {
            return ProcessResult.NotStarted;
        }

        if (process is null)
        {
            return ProcessResult.NotStarted;
        }

        using (process)
        {
            process.StandardInput.Close();
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(timeout))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                return new ProcessResult(Started: true, ExitCode: -1, Output: string.Empty, Error: string.Empty, TimedOut: true);
            }

            process.WaitForExit();
#pragma warning disable RS0030 // the child has exited, so its pipes reach end of file; a grandchild holding one open is what the timeout above bounds
            return new ProcessResult(Started: true, ExitCode: process.ExitCode, Output: output.GetAwaiter().GetResult(), Error: error.GetAwaiter().GetResult(), TimedOut: false);
#pragma warning restore RS0030
        }
    }
}
