using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Redaction;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Infrastructure.Launch;

/// <summary>
/// The 'default-terminal' viewer (#4518): opens whatever terminal the user registered as their default, naming none; the same contract as
/// the bash viewer (scripts/meta-launch.sh viewer_default_terminal). macOS: one executable '.command' file in a private 0700 folder
/// (<c lang="text">rm -f -- "$0"</c>, <c lang="text">cd &lt;cwd&gt; || exit 1</c>, <c lang="text">exec &lt;absolute tmux&gt; &lt;rest of the attach argv&gt;</c>, every word shell-quoted), handed to 'open'. The file removes itself when run; the
/// folder goes once it is empty or after AW_META_COMMAND_GRACE seconds (default 20), in the background so the outcome is not held.
/// Linux: 'xdg-terminal-exec', else 'x-terminal-emulator -e', else a refusal naming both. An exit 0 with no tmux client is unconfirmed, as for exec-template.
/// </summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
internal sealed partial class DefaultTerminalViewer(DefaultTerminalOptions options, ISessionHost host, TimeProvider time, IRedactor? redactor = null) : ITerminalViewer
{
    private const string Xdg = "xdg-terminal-exec";
    private const string XTerminal = "x-terminal-emulator";
    private static readonly TimeSpan _step = TimeSpan.FromMilliseconds(100);
    private readonly ConcurrentBag<Task> _cleanups = new();

    /// <inheritdoc/>
    public string Kind => "default-terminal";

    /// <inheritdoc/>
    public async ValueTask<ViewerOutcome> Open(ViewerRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return options.IsMac ? await OpenMac(request, cancellationToken).ConfigureAwait(false) : await OpenLinux(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Completes when every '.command' folder this viewer made is gone (tests move the fake clock past the grace).</summary>
    internal Task Settled() => Task.WhenAll(_cleanups);

    /// <inheritdoc/>
    public async ValueTask<TerminalFacts> Probe(CancellationToken cancellationToken)
    {
        var found = options.IsMac
            ? await Find("open", cancellationToken).ConfigureAwait(false) is not null
            : await Find(Xdg, cancellationToken).ConfigureAwait(false) is not null || await Find(XTerminal, cancellationToken).ConfigureAwait(false) is not null;
        var gui = ViewerProcess.HasGuiSession(options.GetEnv, options.IsMac);
        var problem = !found ? "not-installed" : gui ? null : "no-gui-session";
        var detail = found ? null : new TerminalDetail(Line: null, options.IsMac ? "open was not found on the PATH" : $"neither {Xdg} nor {XTerminal} was found on the PATH");
        return new TerminalFacts(Kind, "Default terminal", problem is null, "file", ConfigPath: null, problem, detail);
    }

    private async ValueTask<ViewerOutcome> OpenMac(ViewerRequest request, CancellationToken cancellationToken)
    {
        if (!SessionName.IsMatch(request.Host.Name) || request.Host.Name.Contains("..", StringComparison.Ordinal))
        {
            return ViewerProcess.Failed("the session name is not a valid id");
        }

        if (await Find("open", cancellationToken).ConfigureAwait(false) is not { } open)
        {
            return ViewerProcess.Failed("open was not found on the PATH");
        }

        var tmux = await Find("tmux", cancellationToken).ConfigureAwait(false) ?? request.AttachArgv[0];   // a Terminal login shell may lack the caller's PATH
        var folder = Path.Combine(options.TempRoot ?? Path.GetTempPath(), "aw-term-" + Guid.NewGuid().ToString("N"));
        try
        {
            var file = WriteCommandFile(folder, request, tmux);
            return await Show(request, Program(open, file), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return ViewerProcess.Failed($"the launch file could not be written: {e.Message}");
        }
        finally
        {
            _cleanups.Add(Task.Run(() => CleanUp(folder), CancellationToken.None));
        }
    }

    private async ValueTask<ViewerOutcome> OpenLinux(ViewerRequest request, CancellationToken cancellationToken)
    {
        if (await Find(Xdg, cancellationToken).ConfigureAwait(false) is { } xdg)
        {
            return await Show(request, Program(xdg, [.. request.AttachArgv]), cancellationToken).ConfigureAwait(false);
        }

        return await Find(XTerminal, cancellationToken).ConfigureAwait(false) is { } fallback
            ? await Show(request, Program(fallback, ["-e", .. request.AttachArgv]), cancellationToken).ConfigureAwait(false)
            : ViewerProcess.Failed($"no terminal to open: neither {Xdg} nor {XTerminal} is on the PATH (fix: install one, or set viewer.kind in launcher.json)");
    }

    private ValueTask<string?> Find(string program, CancellationToken cancellationToken) =>
        (options.Resolve ?? ViewerProcess.Resolve)(program, cancellationToken);

    private List<string> Program(string path, params string[] arguments) => options.RunWith is { } runner ? [runner, path, .. arguments] : [path, .. arguments];

    private async ValueTask<ViewerOutcome> Show(ViewerRequest request, List<string> argv, CancellationToken cancellationToken)
    {
        var (child, problem) = ViewerProcess.Start(argv, options.Environment, detached: true, workingDirectory: options.IsMac ? null : request.Cwd);
        if (child is null)
        {
            return ViewerProcess.Failed(problem ?? "the viewer did not start");
        }

        try
        {
            return await ViewerProcess.Watch(host, time, new ViewerProcess.WatchPlan(request, () => Failure(child), child.Exited), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            ViewerProcess.Reap(child);   // also when the caller cancels
        }
    }

    private static string WriteCommandFile(string folder, ViewerRequest request, string tmux)
    {
        _ = Directory.CreateDirectory(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var path = Path.Combine(folder, $"sindri-{request.Host.Name}.command");
        var cwd = request.Cwd ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        using var stream = new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
        });
        using var writer = new StreamWriter(stream);
        var attach = string.Join(' ', request.AttachArgv.Skip(1).Select(Quote));
        writer.Write($"#!/bin/sh\nrm -f -- \"$0\"\ncd {Quote(cwd)} || exit 1\nexec {Quote(tmux)} {attach}\n");
        return path;
    }

    /// <summary>One shell word in single quotes; a quote inside it becomes <c lang="text">'\''</c>.</summary>
    internal static string Quote(string word) => "'" + word.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    /// <summary>Waits up to AW_META_COMMAND_GRACE seconds (default 20) for the folder to be empty, then removes it with whatever is left.</summary>
    private async Task CleanUp(string folder)
    {
        var grace = int.TryParse((options.GetEnv ?? Environment.GetEnvironmentVariable)("AW_META_COMMAND_GRACE"), NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ? seconds : 20;
        var deadline = time.GetUtcNow() + TimeSpan.FromSeconds(grace);
        while (Directory.Exists(folder) && Directory.EnumerateFileSystemEntries(folder).Any() && time.GetUtcNow() < deadline)
        {
            await Task.Delay(_step, time).ConfigureAwait(false);
        }

        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            // already gone, or not ours to remove
        }
    }

    private async ValueTask<ViewerOutcome?> Failure(ViewerProcess.Child child)
    {
        if (!child.Exited.IsCompleted || child.Process.ExitCode == 0)
        {
            return null;
        }

        var code = child.Process.ExitCode;
        return ViewerProcess.Failed(await ViewerProcess.Detail(child, code, redactor).ConfigureAwait(false));
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SessionName { get; }
}
