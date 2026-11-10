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
internal sealed class DetachedWaiterLauncher(Func<string?> setsidLocator, Func<string?> selfLocator) : IWaiterLauncher
{
    private const string LogicalProgram = "aw-meta";

    public DetachedWaiterLauncher()
        : this(() => FindOnPath("setsid"), () => Environment.ProcessPath)
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

        var program = Program(argv[0]);
        if (program.IsError)
        {
            return program.Error;
        }

        return Spawn(setsid, program.Value, argv);
    }

    private static Result<int> Spawn(string setsid, string program, IReadOnlyList<string> argv)
    {
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

    // The logical "aw-meta" is the running binary. Under the dotnet host (`dotnet Aw.Meta.Host.dll`, no apphost) that is `dotnet`, and the waiter would run `dotnet lane-recycle ...` and die at once.
    private Result<string> Program(string name)
    {
        if (!string.Equals(name, LogicalProgram, StringComparison.Ordinal))
        {
            return name;
        }

        var self = selfLocator();
        if (string.IsNullOrEmpty(self))
        {
            return MetaErrors.CommandNotStarted.With("--detach can't find the running aw-meta binary: run the installed aw-meta, or recycle the meta by hand");
        }

        return string.Equals(Path.GetFileNameWithoutExtension(self), "dotnet", StringComparison.Ordinal)
            ? MetaErrors.CommandNotStarted.With("--detach needs the published aw-meta binary (this process runs under the dotnet host): run the installed aw-meta, or recycle the meta by hand")
            : self;
    }

    private static string? FindOnPath(string name) =>
        (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(directory => Path.Combine(directory, name))
            .FirstOrDefault(File.Exists);
}
