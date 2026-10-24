using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Desktop;
using Aw.Kernel;

namespace Aw.Evidence.Infrastructure.Desktop;

/// <summary>
/// Starts the repo's app as a child process that outlives the call: the arguments as given (never a shell), the working directory the repo, the
/// environment inherited plus the run's variables. The app needs a display: the user's when DISPLAY is set (and on macOS and Windows, which have
/// one), else a virtual X server through 'xvfb-run -a'; neither is an error that says so. A program with a directory in its name is the working
/// directory's; a bare name is looked up on PATH.
/// </summary>
internal sealed class ProcessDesktopApps : IDesktopApps
{
    private const string VirtualScreen = "-screen 0 1920x1200x24";

    /// <inheritdoc/>
    public ValueTask<Result<IRunningApp>> StartAsync(AppLaunch launch, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var environment = Merged(launch.Environment);
        var display = DisplayFor(environment);
        if (display.IsError)
        {
            return ValueTask.FromResult<Result<IRunningApp>>(display.Error);
        }

        var program = ProgramOf(launch);
        if (program.Contains('/', StringComparison.Ordinal) && !File.Exists(program))
        {
            return ValueTask.FromResult<Result<IRunningApp>>(EvidenceErrors.RunFailed.With($"{launch.Argv[0]} could not start: no such file"));
        }

        string[] argv = [.. display.Value.Wrapper, program, .. launch.Argv.Skip(1)];
        return ValueTask.FromResult(Start(argv, launch, display.Value.Note));
    }

    private static Result<IRunningApp> Start(string[] argv, AppLaunch launch, string note)
    {
        var start = new ProcessStartInfo(argv[0])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = launch.WorkingDirectory,
        };
        foreach (var argument in argv.Skip(1))
        {
            start.ArgumentList.Add(argument);
        }

        foreach (var (name, value) in launch.Environment)
        {
            start.Environment[name] = value;
        }

        var process = new Process { StartInfo = start };
        try
        {
            _ = process.Start();
        }
        catch (Win32Exception ex)
        {
            process.Dispose();
            return EvidenceErrors.RunFailed.With($"{launch.Argv[0]} could not start: {ex.Message}");
        }

        return new ProcessRunningApp(process, note);
    }

    private static Result<(string[] Wrapper, string Note)> DisplayFor(Dictionary<string, string> environment)
    {
        if (!OperatingSystem.IsLinux())
        {
            return (Wrapper: Array.Empty<string>(), Note: "display: the desktop session");
        }

        if (environment.TryGetValue("DISPLAY", out var display) && display.Length > 0)
        {
            return (Wrapper: Array.Empty<string>(), Note: $"display: DISPLAY={display} (the user's)");
        }

        return OnPath("xvfb-run", environment.GetValueOrDefault("PATH")) is { } xvfb
            ? (Wrapper: new[] { xvfb, "-a", "-s", VirtualScreen }, Note: "display: xvfb-run (virtual X server 1920x1200x24)")
            : EvidenceErrors.RunFailed.With("no DISPLAY and no xvfb-run on PATH (install xvfb to run the app headless)");
    }

    private static string? OnPath(string name, string? path) =>
        (path ?? string.Empty).Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Select(directory => Path.Combine(directory, name)).FirstOrDefault(File.Exists);

    private static Dictionary<string, string> Merged(IReadOnlyDictionary<string, string> extra)
    {
        Dictionary<string, string> environment = new(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            environment[(string)entry.Key] = entry.Value as string ?? string.Empty;
        }

        foreach (var (name, value) in extra)
        {
            environment[name] = value;
        }

        return environment;
    }

    private static string ProgramOf(AppLaunch launch)
    {
        var program = launch.Argv[0];
        return program.Contains('/', StringComparison.Ordinal) && !program.StartsWith('/') ? Path.Combine(launch.WorkingDirectory, program) : program;
    }
}
