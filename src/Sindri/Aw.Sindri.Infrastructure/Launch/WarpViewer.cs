using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Redaction;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The 'warp' viewer (API P10a, unit U6; UI spec 6.3): one launch configuration per launch ('sindri-&lt;session&gt;.yaml',
/// written with the YAML emitter, mode 0600, the repo path only in 'cwd'), opened through 'warp://launch/…' by 'open' or
/// 'xdg-open', deleted 60 s after the outcome, and any older one swept at start-up so an old launch can't be re-run from
/// Warp's palette. The window and the client check are the shared ones.
/// </summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
internal sealed partial class WarpViewer(WarpViewerOptions options, ISessionHost host, TimeProvider time, IRedactor? redactor = null) : ITerminalViewer
{
    private static readonly TimeSpan _keep = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan _openerBudget = TimeSpan.FromSeconds(5);
    private readonly ConcurrentDictionary<string, ITimer> _timers = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public string Kind => "warp";

    /// <inheritdoc/>
    public async ValueTask<ViewerOutcome> Open(ViewerRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var name = request.Host.Name;
        if (!SessionName.IsMatch(name) || name.Contains("..", StringComparison.Ordinal))
        {
            return ViewerProcess.Failed("the session name is not a valid id");
        }

        var file = $"sindri-{name}.yaml";
        var path = Path.Combine(options.LaunchDir, file);
        try
        {
            Write(path, request, name);
            return await Show(request, file, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException e)
        {
            return ViewerProcess.Failed($"the launch file could not be written: {e.Message}");
        }
        catch (UnauthorizedAccessException e)
        {
            return ViewerProcess.Failed($"the launch file could not be written: {e.Message}");
        }
        finally
        {
            Schedule(path);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<TerminalFacts> Probe(CancellationToken cancellationToken)
    {
        var opener = await ViewerProcess.OnLoginPath(options.Opener, cancellationToken).ConfigureAwait(false);
        var handler = opener && (options.IsMac || await HasLinuxHandler(cancellationToken).ConfigureAwait(false));
        var gui = ViewerProcess.HasGuiSession(options.GetEnv);
        var problem = !handler ? "not-installed" : gui ? null : "no-gui-session";
        return new TerminalFacts(Kind, "Warp", problem is null, "file", ConfigPath: null, problem, Detail: null);
    }

    /// <summary>Deletes launch files older than 60 s (the launcher service's start-up sweep).</summary>
    public void Sweep()
    {
        if (!Directory.Exists(options.LaunchDir))
        {
            return;
        }

        var cutoff = time.GetUtcNow().UtcDateTime - _keep;
        foreach (var file in Directory.EnumerateFiles(options.LaunchDir, "sindri-*.yaml"))
        {
            if (File.GetLastWriteTimeUtc(file) < cutoff)
            {
                TryDelete(file);
            }
        }
    }

    private async ValueTask<ViewerOutcome> Show(ViewerRequest request, string file, CancellationToken cancellationToken)
    {
        var (child, problem) = ViewerProcess.Start([options.Opener, "warp://launch/" + file], environment: null);
        if (child is null)
        {
            return ViewerProcess.Failed(problem ?? "the opener did not start");
        }

        try
        {
            using var budget = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            budget.CancelAfter(_openerBudget);
            try
            {
                await child.Exited.WaitAsync(budget.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return ViewerOutcome.Failed(SindriErrors.LaunchTimedOut.Code, string.Create(CultureInfo.InvariantCulture, $"{options.Opener} did not return within {_openerBudget.TotalSeconds:0} s"));
            }

            var code = child.Process.ExitCode;
            if (code != 0)
            {
                return ViewerProcess.Failed(await ViewerProcess.Detail(child, code, redactor).ConfigureAwait(false));
            }

            return await ViewerProcess.Watch(host, time, new ViewerProcess.WatchPlan(request, () => ValueTask.FromResult<ViewerOutcome?>(null), Wake: null), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ViewerProcess.Reap(child);   // on every way out, including the caller's cancel
        }
    }

    private void Write(string path, ViewerRequest request, string name)
    {
        _ = Directory.CreateDirectory(options.LaunchDir);
        TryDelete(path);
        var cwd = request.Cwd ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        using var stream = new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
        });
        using var writer = new StreamWriter(stream);
        WarpLaunchFile.Emit(writer, name, request.Title, cwd, string.Join(' ', request.AttachArgv.Select(WarpLaunchFile.Quote)));
    }

    private void Schedule(string path)
    {
        var timer = time.CreateTimer(
            state =>
            {
                if (state is not string file)
                {
                    return;
                }

                TryDelete(file);
                if (_timers.TryRemove(file, out var done))
                {
                    done.Dispose();
                }
            },
            state: path,
            dueTime: _keep,
            period: Timeout.InfiniteTimeSpan);
        if (_timers.TryAdd(path, timer))
        {
            return;
        }

        timer.Dispose();   // a retry of the same launch: the first timer already covers the file
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // already gone or busy: the sweep takes it next time
        }
        catch (UnauthorizedAccessException)
        {
            // not ours to remove
        }
    }

    private static async ValueTask<bool> HasLinuxHandler(CancellationToken cancellationToken) =>
        await ViewerProcess.Output("xdg-mime", ["query", "default", "x-scheme-handler/warp"], cancellationToken).ConfigureAwait(false) is { Length: > 0 };

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SessionName { get; }
}
