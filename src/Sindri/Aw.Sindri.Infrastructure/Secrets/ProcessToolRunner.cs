using System.ComponentModel;
using System.Diagnostics;

namespace Aw.Sindri.Infrastructure.Secrets;

/// <summary>IToolRunner over System.Diagnostics.Process: no shell, standard output and standard error captured.</summary>
internal sealed class ProcessToolRunner : IToolRunner
{
    /// <inheritdoc />
    public ToolRun Run(string fileName, IReadOnlyList<string> arguments, TimeSpan timeout)
    {
        var start = new ProcessStartInfo(fileName, arguments)
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
            return ToolRun.Missing;   // not on PATH
        }

        if (process is null)
        {
            return ToolRun.Missing;
        }

        using (process)
        {
            return Wait(process, timeout);
        }
    }

    private static ToolRun Wait(Process process, TimeSpan timeout)
    {
        process.StandardInput.Close();   // a tool that prompts reads end of input instead of waiting for a terminal
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(timeout))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            return ToolRun.TimedOut;
        }

        process.WaitForExit();   // flushes the redirected streams
        return new ToolRun(ToolOutcome.Exited, process.ExitCode, output.GetAwaiter().GetResult(), error.GetAwaiter().GetResult());
    }
}
