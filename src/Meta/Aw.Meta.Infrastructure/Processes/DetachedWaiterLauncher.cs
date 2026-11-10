using System.Diagnostics;
using Aw.Kernel;
using Aw.Meta.Core;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Infrastructure.Processes;

/// <summary>
/// IWaiterLauncher over a child process that outlives the caller: the command runs under setsid (its own session, so the end of the
/// caller's turn or shell can't signal it) with stdin, stdout and stderr sent to /dev/null by an sh that execs the command. The program name "aw-meta" stands for the running
/// binary (<see cref="Environment.ProcessPath"/>). Where there is no setsid (macOS) the child starts without it.
/// </summary>
internal sealed class DetachedWaiterLauncher : IWaiterLauncher
{
    private const string LogicalProgram = "aw-meta";

    public ValueTask<Result<int>> Start(IReadOnlyList<string> argv, CancellationToken cancellationToken) => new(StartNow(argv));

    private static Result<int> StartNow(IReadOnlyList<string> argv)
    {
        ArgumentNullException.ThrowIfNull(argv);
        if (argv.Count == 0)
        {
            return MetaErrors.CommandNotStarted.With("empty command");
        }

        var program = string.Equals(argv[0], LogicalProgram, StringComparison.Ordinal) && Environment.ProcessPath is { Length: > 0 } self ? self : argv[0];
        var setsid = FindOnPath("setsid");

        // sh redirects the three streams to /dev/null and execs the command, so the pid is the command's own and nothing it prints can break a pipe.
        var info = new ProcessStartInfo(setsid ?? "sh") { UseShellExecute = false, CreateNoWindow = true };
        if (setsid is not null)
        {
            info.ArgumentList.Add("sh");
        }

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
