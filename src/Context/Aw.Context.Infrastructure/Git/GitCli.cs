using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Aw.Context.Core;
using Aw.Kernel;

namespace Aw.Context.Infrastructure.Git;

/// <summary>
/// Read-only git calls for the repo-docs source (spec §5.1 argument rules): shas match ^[0-9a-f]{7,64}$ and never start
/// with '-', every path comes after '--', the arguments go through ArgumentList (no shell), and git runs with the
/// caller's GIT_* variables removed. A process that misses its time (30 s, or the caller's token) is killed. A missing
/// object named by a full sha (what ls-tree gives) is NotFound (SND-CONTEXT-003); any other git failure, an unknown
/// abbreviated sha included, is SND-CONTEXT-010.
/// </summary>
internal sealed partial class GitCli
{
    private readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _archiveTimeout = TimeSpan.FromMinutes(30);
    private readonly IReadOnlyList<string> _command;
    private readonly TimeSpan _drainGrace;

    /// <summary>The git on PATH, with the shipped two-second grace for stderr after git exits.</summary>
    public GitCli()
        : this(["git"], TimeSpan.FromSeconds(2))
    {
    }

    /// <summary>A git reached by another command, and another stderr grace: what the tests use to stand in a git that exits while something else still holds its stderr.</summary>
    /// <param name="command">The command and its leading arguments; the git arguments follow them.</param>
    /// <param name="drainGrace">How long a finished git's stderr may stay open before the call stops waiting for it.</param>
    internal GitCli(IReadOnlyList<string> command, TimeSpan drainGrace)
    {
        _command = command;
        _drainGrace = drainGrace;
    }

    /// <summary>The commit HEAD points at.</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The sha.</returns>
    public async ValueTask<Result<string>> Head(string repo, CancellationToken cancellationToken)
    {
        var head = await Text(repo, ["rev-parse", "--verify", "--quiet", "HEAD^{commit}"], cancellationToken).ConfigureAwait(false);
        return head.Bind<string>(h => IsSha(h.Trim()) ? h.Trim() : ContextErrors.SourceUnavailable.With("git rev-parse HEAD didn't answer a sha"));
    }

    /// <summary>Every blob at <paramref name="commit"/>: path to blob sha.</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="commit">A commit sha.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The tree, '/'-separated paths.</returns>
    public async ValueTask<Result<IReadOnlyDictionary<string, string>>> Tree(string repo, string commit, CancellationToken cancellationToken)
    {
        if (!IsSha(commit))
        {
            return ContextErrors.SourceUnavailable.With("not a commit sha");
        }

        var listed = await Text(repo, ["ls-tree", "-r", "-z", "--full-tree", commit], cancellationToken).ConfigureAwait(false);
        return listed.Map<IReadOnlyDictionary<string, string>>(text => text.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(entry => entry.Split('\t', 2))
            .Where(parts => parts.Length == 2 && parts[0].Split(' ') is [_, "blob", _])
            .ToDictionary(parts => parts[1], parts => parts[0].Split(' ')[2], StringComparer.Ordinal));
    }

    /// <summary>A blob's size in bytes, read before its text so an oversized document is never loaded.</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="blob">The blob sha.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The size, NotFound when the repo has no such blob, or SND-CONTEXT-010.</returns>
    public async ValueTask<Result<long>> Size(string repo, string blob, CancellationToken cancellationToken)
    {
        var exists = await Exists(repo, blob, cancellationToken).ConfigureAwait(false);
        var size = exists.IsError ? exists.Error : await Text(repo, ["cat-file", "-s", blob], cancellationToken).ConfigureAwait(false);
        return size.Bind<long>(s => long.TryParse(s.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var bytes)
            ? bytes
            : ContextErrors.SourceUnavailable.With("git cat-file -s didn't answer a size"));
    }

    /// <summary>A blob's text.</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="blob">The blob sha.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The text, NotFound (SND-CONTEXT-003) when the repo has no such blob, or SND-CONTEXT-010.</returns>
    public async ValueTask<Result<string>> Blob(string repo, string blob, CancellationToken cancellationToken)
    {
        var exists = await Exists(repo, blob, cancellationToken).ConfigureAwait(false);
        return exists.IsError ? exists.Error : await Text(repo, ["cat-file", "blob", blob], cancellationToken).ConfigureAwait(false);
    }

    /// <summary>A file's text at a commit ('cat-file blob &lt;commit&gt;:&lt;path&gt;'). The caller has checked <paramref name="path"/> (relative, no '..').</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="commit">A commit sha.</param>
    /// <param name="path">The path from the repo root.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The text, or SND-CONTEXT-010 when git can't read it.</returns>
    public async ValueTask<Result<string>> Show(string repo, string commit, string path, CancellationToken cancellationToken) =>
        IsSha(commit)
            ? await Text(repo, ["cat-file", "blob", $"{commit}:{path}"], cancellationToken).ConfigureAwait(false)
            : ContextErrors.SourceUnavailable.With("not a commit sha");

    /// <summary>Streams 'git archive --format=tar' of a commit to <paramref name="consume"/> (tracked files only, never the working tree).</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="commit">A commit sha.</param>
    /// <param name="consume">Reads the tar stream to its end.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Unit, or SND-CONTEXT-010 when git fails or the consumer does.</returns>
    public async ValueTask<Result<Unit>> Archive(string repo, string commit, Func<Stream, CancellationToken, Task> consume, CancellationToken cancellationToken)
    {
        if (!IsSha(commit))
        {
            return ContextErrors.SourceUnavailable.With("not a commit sha");
        }

        var start = CleanGit(["-C", repo, "archive", "--format=tar", commit]);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_archiveTimeout);
        Process? git = null;
        try
        {
            git = Process.Start(start) ?? throw new InvalidOperationException("git didn't start");
            var stderr = git.StandardError.ReadToEndAsync(timeout.Token);
            await ConsumeToEnd(git.StandardOutput.BaseStream, consume, timeout.Token).ConfigureAwait(false);
            await git.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            // git is gone, so everything it wrote is already in the pipe. Its end of stderr closed with it; a stray fork of a parallel
            // process may still hold another copy, and waiting for that EOF held this call to the whole archive timeout (a gate hung 5 min here).
            _ = await Task.WhenAny(stderr, Task.Delay(_drainGrace, timeout.Token)).ConfigureAwait(false);
            return git.ExitCode == 0 ? Unit.Value : Failed("archive", git.ExitCode);
        }
        catch (OperationCanceledException)
        {
            Kill(git);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return ContextErrors.SourceUnavailable.With("git archive didn't finish in time");
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception or IOException or InvalidDataException)
        {
            Kill(git);
            return ContextErrors.SourceUnavailable.With($"git archive didn't run: {e.GetType().Name}");
        }
        finally
        {
            git?.Dispose();
        }
    }

    // A TarReader stops at the end-of-archive marker and leaves git's record padding in the pipe. On a loaded box (the user over
    // pipe-user-pages-soft) a new pipe holds one page, so git blocked writing the rest and never exited (a gate hung here 30 s).
    private static async Task ConsumeToEnd(Stream tar, Func<Stream, CancellationToken, Task> consume, CancellationToken cancellationToken)
    {
        await consume(tar, cancellationToken).ConfigureAwait(false);
        await tar.CopyToAsync(Stream.Null, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>True for a full or abbreviated lower-case hex sha (never an option, never a ref expression).</summary>
    /// <param name="text">The candidate.</param>
    /// <returns>Whether it is a sha.</returns>
    internal static bool IsSha(string text) => Sha.IsMatch(text);

    /// <summary>Whether the blob exists: for a full sha 'cat-file -e' exits 1 when the object is missing, and 128 when the repo can't be read (or an abbreviated sha is unknown).</summary>
    private async ValueTask<Result<Unit>> Exists(string repo, string blob, CancellationToken cancellationToken)
    {
        if (!IsSha(blob))
        {
            return ContextErrors.UnknownAnchor.With("not a blob sha");
        }

        var run = await Run(repo, ["cat-file", "-e", blob], cancellationToken).ConfigureAwait(false);
        return run switch
        {
            { IsError: true } => run.Error,
            { Value.Exit: 0 } => Unit.Value,
            { Value.Exit: 1 } => ContextErrors.UnknownAnchor.With("the repo has no such blob"),
            _ => Failed("cat-file", run.Value.Exit),
        };
    }

    private async ValueTask<Result<string>> Text(string repo, string[] args, CancellationToken cancellationToken)
    {
        var run = await Run(repo, args, cancellationToken).ConfigureAwait(false);
        return run.IsError ? run.Error : run.Value.Exit == 0 ? run.Value.Stdout : Failed(args[0], run.Value.Exit);
    }

    private async ValueTask<Result<(int Exit, string Stdout)>> Run(string repo, string[] args, CancellationToken cancellationToken, string? locale = null)
    {
        var start = new ProcessStartInfo(_command[0])
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            StandardOutputEncoding = Encoding.UTF8,
        };
        foreach (var name in start.Environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.Ordinal)).ToList())
        {
            _ = start.Environment.Remove(name);
        }

        if (locale is not null)
        {
            start.Environment["LC_ALL"] = locale;   // case folding of 'grep -i' follows the locale
        }

        foreach (var arg in (string[])[.. _command.Skip(1), "-C", repo, "-c", "core.quotepath=off", .. args])
        {
            start.ArgumentList.Add(arg);
        }

        return await Execute(start, args[0], cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<Result<(int Exit, string Stdout)>> Execute(ProcessStartInfo start, string verb, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        Process? git = null;
        try
        {
            git = Process.Start(start) ?? throw new InvalidOperationException("git didn't start");
            var stdout = git.StandardOutput.ReadToEndAsync(timeout.Token);
            var stderr = git.StandardError.ReadToEndAsync(timeout.Token);
            await git.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            _ = await stderr.ConfigureAwait(false);   // drained, never logged: it may name repo content
            return (git.ExitCode, await stdout.ConfigureAwait(false));
        }
        catch (OperationCanceledException)
        {
            Kill(git);   // a git call never outlives the query or its own time
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return ContextErrors.SourceUnavailable.With($"git {verb} didn't finish in time");
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return ContextErrors.SourceUnavailable.With($"git {verb} didn't run: {e.GetType().Name}");
        }
        finally
        {
            git?.Dispose();
        }
    }

    private ProcessStartInfo CleanGit(string[] args)
    {
        var start = new ProcessStartInfo(_command[0], [.. _command.Skip(1), .. args]) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var name in start.Environment.Keys.Where(k => k.StartsWith("GIT_", StringComparison.Ordinal)).ToList())
        {
            _ = start.Environment.Remove(name);
        }

        return start;
    }

    private static void Kill(Process? git)
    {
        try
        {
            if (git is { HasExited: false })
            {
                git.Kill(entireProcessTree: true);
            }
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // it exited between the check and the kill
        }
    }

    private static Error Failed(string verb, int exit) =>
        ContextErrors.SourceUnavailable.With(string.Create(CultureInfo.InvariantCulture, $"git {verb} failed (exit {exit})"));

    [GeneratedRegex("^[0-9a-f]{7,64}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Sha { get; }
}
