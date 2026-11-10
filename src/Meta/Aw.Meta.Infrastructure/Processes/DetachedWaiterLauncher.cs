using System.Diagnostics;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Processes;

/// <summary>
/// IWaiterLauncher over a child process that outlives the caller: the command runs under setsid (its own session, so the end of the
/// caller's turn or shell can't signal it) with stdin, stdout and stderr sent to /dev/null by an sh that execs the command. The program name "aw-meta" stands for the running
/// binary (<see cref="Environment.ProcessPath"/>). Where there is no setsid (stock macOS) nothing starts: the child would stay in the caller's process group and could die with the caller's shell, so the start is AW-META-010.
/// </summary>
internal sealed class DetachedWaiterLauncher(Func<string?> setsidLocator) : IWaiterLauncher
{
    private const string LogicalProgram = "aw-meta";

    public DetachedWaiterLauncher()
        : this(() => FindOnPath("setsid"))
    {
    }

    public ValueTask<Result<int>> Start(IReadOnlyList<string> argv, CancellationToken cancellationToken) => new(StartNow(argv));

    private Result<int> StartNow(IReadOnlyList<string> argv)
    {
        ArgumentNullException.ThrowIfNull(argv);
        if (argv.Count == 0)
        {
            return MetaErrors.CommandNotStarted.With("empty command");
        }

        var setsid = setsidLocator();
        if (setsid is null)
        {
            var os = OperatingSystem.IsMacOS() ? "macOS" : OperatingSystem.IsLinux() ? "Linux" : "this OS";
            return MetaErrors.HostUnreadable.With($"unavailable on {os}: setsid not found, so the waiter can't outlive this turn; recycle the meta by hand");
        }

        var program = string.Equals(argv[0], LogicalProgram, StringComparison.Ordinal) && Environment.ProcessPath is { Length: > 0 } self ? self : argv[0];
        // sh redirects the three streams to /dev/null and execs the command, so the pid is the command's own and nothing it prints can break a pipe.
        var info = new ProcessStartInfo(setsid) { UseShellExecute = false, CreateNoWindow = true };
        info.ArgumentList.Add("sh");

        info.ArgumentList.Add("-c");
        info.ArgumentList.Add("exec \"$@\" </dev/null >/dev/null 2>&1");
        info.ArgumentList.Add("aw-meta-waiter");
        info.ArgumentList.Add(program);
        foreach (var argument in argv.Skip(1))
        {
            info.ArgumentList.Add(argument);
        }

        try
        {
            using var process = Process.Start(info);
            return process is null ? MetaErrors.CommandNotStarted.With($"{argv[0]} did not start") : Results.Success(process.Id);
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return MetaErrors.CommandNotStarted.With($"{argv[0]} did not start: {exception.Message}");
        }
    }

    private static string? FindOnPath(string name) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, name))
            .FirstOrDefault(File.Exists);
}
