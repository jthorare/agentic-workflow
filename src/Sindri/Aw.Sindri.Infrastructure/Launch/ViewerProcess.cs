using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using Aw.Redaction;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Infrastructure.Processes;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>What the viewers share: one spawned process with a bounded stderr, the observation window, GUI and program checks.</summary>
internal static partial class ViewerProcess
{
    internal const int StderrLimit = 2048;
    private static readonly TimeSpan _poll = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan _probeBudget = TimeSpan.FromSeconds(5);

    // A terminal that outlives Sindri must not share its session or its pipes (launcher spec: new session, stdin and stdout /dev/null, stderr in
    // its own file): sh redirects all three, then starts the program under setsid where the box has it (macOS has none, so there it only detaches the descriptors).
    private const string DetachScript = "err=$1; shift; exec </dev/null >/dev/null 2>\"$err\"; if command -v setsid >/dev/null 2>&1; then exec setsid \"$@\"; else exec \"$@\"; fi";

    private static ProcessStartInfo Info(IReadOnlyList<string> argv, IReadOnlyDictionary<string, string>? environment, string? errorFile, string? workingDirectory)
    {
        var detached = errorFile is not null;
        var info = ChildEnvironment.Start(detached ? "/bin/sh" : argv[0]);
        info.RedirectStandardInput = true;
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = !detached;
        if (detached)
        {
            info.ArgumentList.Add("-c");
            info.ArgumentList.Add(DetachScript);
            info.ArgumentList.Add("sh");
            info.ArgumentList.Add(errorFile ?? string.Empty);   // detached implies a file; the fallback is never used
            info.ArgumentList.Add(argv[0]);
        }

        foreach (var argument in argv.Skip(1))
        {
            info.ArgumentList.Add(argument);
        }

        if (workingDirectory is not null)
        {
            info.WorkingDirectory = workingDirectory;
        }

        _ = info.Environment.Remove("TMUX");   // a viewer must never reach the tmux of whoever started Sindri
        foreach (var (key, value) in environment ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            info.Environment[key] = value;
        }

        return info;
    }

    /// <summary>Starts the program (argv[0]) with a clean TMUX; null when it can't be started (the message says why).</summary>
    /// <param name="argv">The program and its arguments, no shell.</param>
    /// <param name="environment">Extra environment, or null.</param>
    /// <param name="workingDirectory">Where the child starts; this process's directory when null.</param>
    /// <param name="detached">True for a terminal window that may outlive Sindri: its own session, stdin and stdout on /dev/null; stderr stays piped for the window.</param>
    /// <returns>The child, or the reason it could not start.</returns>
    internal static (Child? Child, string? Problem) Start(IReadOnlyList<string> argv, IReadOnlyDictionary<string, string>? environment, bool detached = false, string? workingDirectory = null)
    {
        var errorFile = detached ? Path.GetTempFileName() : null;   // 0600, in the temp directory; deleted when the window's process ends
        var info = Info(argv, environment, errorFile, workingDirectory);
        try
        {
            var process = Process.Start(info);
            if (process is null)
            {
                DeleteQuietly(errorFile);
                return (null, $"{argv[0]} did not start");
            }

            process.StandardInput.Close();
            process.OutputDataReceived += static (_, _) => { };
            process.BeginOutputReadLine();
            var child = new Child(process) { ErrorFile = errorFile };
            child.Drain();
            return (child, null);
        }
        catch (Win32Exception e)
        {
            DeleteQuietly(errorFile);
            return (null, $"{argv[0]} could not be started: {e.Message}");
        }
    }

    /// <summary>The stderr after the child ended (a moment for the pipe to close), scrubbed; the exit code when there was none.</summary>
    internal static async ValueTask<string> Detail(Child child, int exitCode, IRedactor? redactor)
    {
        try
        {
            await child.Drained.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // a grandchild kept the pipe open: take what arrived
        }

        var text = child.Stderr().Trim();
        return text.Length == 0 ? string.Create(CultureInfo.InvariantCulture, $"exited with code {exitCode}") : (redactor ?? Redactor.Default).Scrub(text).Text;
    }

    /// <summary>Reaps a child that outlives the window (a terminal window): waits for it to end, then disposes it.</summary>
    internal static void Reap(Child child) => _ = Task.Run(async () =>
    {
        try
        {
            await child.Exited.ConfigureAwait(false);
            await child.Drained.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // fall through to Dispose
        }
        finally
        {
            child.Process.Dispose();
            DeleteQuietly(child.ErrorFile);
        }
    });

    /// <summary>What <see cref="Watch"/> waits on: the request, a check for a viewer that already failed, and a task that ends when it does.</summary>
    /// <param name="Request">The viewer request (the session and the window).</param>
    /// <param name="Early">Answers a failure outcome once the viewer has failed, else null.</param>
    /// <param name="Wake">Completes when the viewer process ends, so it is looked at at once; null when there is none.</param>
    internal sealed record WatchPlan(ViewerRequest Request, Func<ValueTask<ViewerOutcome?>> Early, Task? Wake);

    /// <summary>
    /// The 3 s window: the first attached client is opened; the plan's the early check (a viewer that already failed) ends it;
    /// the end of the window with neither is unconfirmed. Polls every 250 ms on <paramref name="time"/>.
    /// </summary>
    internal static async ValueTask<ViewerOutcome> Watch(ISessionHost host, TimeProvider time, WatchPlan plan, CancellationToken cancellationToken)
    {
        var (request, early, wake) = (plan.Request, plan.Early, plan.Wake);
        var session = request.Host;
        var window = request.Window;
        var deadline = time.GetUtcNow() + window;
        while (true)
        {
            if (await host.AttachedClients(session, cancellationToken).ConfigureAwait(false) > 0)
            {
                return ViewerOutcome.Opened;
            }

            if (await early().ConfigureAwait(false) is { } failed)
            {
                return failed;
            }

            if (time.GetUtcNow() >= deadline)
            {
                return ViewerOutcome.Unconfirmed;
            }

            using var step = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var tick = Task.Delay(_poll, time, step.Token);
            if (wake is { IsCompleted: false })
            {
                _ = await Task.WhenAny(wake, tick).ConfigureAwait(false);   // a viewer that ends is looked at now, not at the next tick
                await step.CancelAsync().ConfigureAwait(false);
            }
            else
            {
                await tick.ConfigureAwait(false);
            }
        }
    }

    internal static ViewerOutcome Failed(string detail) => ViewerOutcome.Failed(SindriErrors.LaunchViewerFailed.Code, detail);

    /// <summary>Resolves a program through the login shell, with the name as a positional argument (never interpolated).</summary>
    internal static async ValueTask<bool> OnLoginPath(string program, CancellationToken cancellationToken) =>
        await Output("bash", ["-lc", "command -v -- \"$1\"", "_", program], cancellationToken).ConfigureAwait(false) is { Length: > 0 };

    /// <summary>The path the login shell finds for a program, or null when it is missing.</summary>
    internal static async ValueTask<string?> Resolve(string program, CancellationToken cancellationToken) =>
        await Output("bash", ["-lc", "command -v -- \"$1\"", "_", program], cancellationToken).ConfigureAwait(false) is { Length: > 0 } path ? path : null;

    /// <summary>The stdout of a short query (5 s) that exited 0; null when it failed, wasn't found or ran over (a query that runs over is killed with its children).</summary>
    internal static ValueTask<string?> Output(string program, IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        Output(program, arguments, _probeBudget, cancellationToken);

    /// <summary>As <see cref="Output(string, IReadOnlyList{string}, CancellationToken)"/> with its own <paramref name="budget"/>.</summary>
    /// <param name="program">The program (no shell).</param>
    /// <param name="arguments">Its arguments.</param>
    /// <param name="budget">How long it may run.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The trimmed stdout, or null.</returns>
    internal static async ValueTask<string?> Output(string program, IReadOnlyList<string> arguments, TimeSpan budget, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(budget);
        var info = ChildEnvironment.Start(program);
        info.RedirectStandardOutput = true;
        info.RedirectStandardError = true;
        info.RedirectStandardInput = true;
        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        _ = info.Environment.Remove("TMUX");
        Process? process = null;
        try
        {
            process = Process.Start(info);
            return process is null ? null : await ReadOutput(process, deadline.Token).ConfigureAwait(false);
        }
        catch (Win32Exception)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        finally
        {
            Kill(process);
        }
    }

    private static async Task<string?> ReadOutput(Process process, CancellationToken deadline)
    {
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        _ = process.StandardError.ReadToEndAsync(CancellationToken.None);
        await process.WaitForExitAsync(deadline).ConfigureAwait(false);
        var text = await output.WaitAsync(deadline).ConfigureAwait(false);   // a backgrounded child that holds stdout open can't hold the call past its budget
        return process.ExitCode == 0 ? text.Trim() : null;
    }

    /// <summary>Ends a process that is still running, with its children; one that already ended, or can't be signalled, is left alone.</summary>
    /// <param name="process">The process, or null.</param>
    internal static void Kill(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception e) when (e is InvalidOperationException or Win32Exception)
        {
            // it ended between the check and the kill
        }
        finally
        {
            process.Dispose();
        }
    }

    internal static bool HasGuiSession(Func<string, string?>? getEnv, bool isMac)
    {
        if (isMac)
        {
            return true;
        }

        var read = getEnv ?? Environment.GetEnvironmentVariable;
        return !string.IsNullOrEmpty(read("DISPLAY")) || !string.IsNullOrEmpty(read("WAYLAND_DISPLAY"));
    }
}
