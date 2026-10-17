using System.ComponentModel;
using System.Diagnostics;
using Aw.Kernel;
using Aw.Sindri.Core;

namespace Aw.Sindri.Host.Commands.SessionWrap;

/// <summary>Starts the agent as a child process on wrap's own terminal (its stdin, stdout and stderr are wrap's).</summary>
internal sealed class ProcessChildLauncher : IChildLauncher
{
    public Result<IWrapChild> Start(IReadOnlyList<string> argv, IReadOnlyDictionary<string, string> environment)
    {
        ArgumentNullException.ThrowIfNull(argv);
        ArgumentNullException.ThrowIfNull(environment);
        var info = new ProcessStartInfo(argv[0]) { UseShellExecute = false };
        foreach (var argument in argv.Skip(1))
        {
            info.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in environment)
        {
            info.Environment[name] = value;
        }

        try
        {
            var process = Process.Start(info);
            return process is null
                ? SindriErrors.LaunchAgentMissing.With($"{argv[0]} did not start (fix: run `sindri launch show`)")
                : new ProcessChild(process);
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
            return SindriErrors.LaunchAgentMissing.With($"{argv[0]} did not start: {e.Message} (fix: run `sindri launch show`)");
        }
    }

    private sealed class ProcessChild : IWrapChild
    {
        private readonly Process _process;
        private readonly TaskCompletionSource<int> _exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal ProcessChild(Process process)
        {
            _process = process;
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => _exited.TrySetResult(Code());
            if (process.HasExited)
            {
                _ = _exited.TrySetResult(Code());
            }
        }

        public Task<int> Exited => _exited.Task;

        public void Dispose()
        {
            try
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);   // this one handle: never a pattern
                    _ = _process.WaitForExit(5000);
                }
            }
            catch (InvalidOperationException)
            {
                // already gone
            }

            _ = _exited.TrySetResult(137);
            _process.Dispose();
        }

        private int Code()
        {
            try
            {
                return _process.ExitCode;
            }
            catch (InvalidOperationException)
            {
                return 1;
            }
        }
    }
}
