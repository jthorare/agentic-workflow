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
        var listed = await TreeWithLinks(repo, commit, cancellationToken).ConfigureAwait(false);
        return listed.Map(l => l.Blobs);
    }

    /// <summary>Every blob at <paramref name="commit"/> as <see cref="Tree"/> lists them, and which of those paths are symlinks (ls-tree mode 120000).</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="commit">A commit sha.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The tree and its symlink paths, '/'-separated.</returns>
    public async ValueTask<Result<(IReadOnlyDictionary<string, string> Blobs, IReadOnlySet<string> Symlinks)>> TreeWithLinks(string repo, string commit, CancellationToken cancellationToken)
    {
        if (!IsSha(commit))
        {
            return ContextErrors.SourceUnavailable.With("not a commit sha");
        }

        var listed = await Text(repo, ["ls-tree", "-r", "-z", "--full-tree", commit], cancellationToken).ConfigureAwait(false);
        return listed.Map<(IReadOnlyDictionary<string, string>, IReadOnlySet<string>)>(text =>
        {
            var entries = text.Split('\0', StringSplitOptions.RemoveEmptyEntries)
                .Select(entry => entry.Split('\t', 2))
                .Where(parts => parts.Length == 2 && parts[0].Split(' ') is [_, "blob", _])
                .Select(parts => (Path: parts[1], Fields: parts[0].Split(' ')))
                .ToList();
            return (entries.ToDictionary(e => e.Path, e => e.Fields[2], StringComparer.Ordinal),
                entries.Where(e => e.Fields[0] == "120000").Select(e => e.Path).ToHashSet(StringComparer.Ordinal));
        });
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

    /// <summary>A blob's raw bytes, so a hash of them equals the hash of the file as committed (no text decoding).</summary>
    /// <param name="repo">The repo's checkout.</param>
    /// <param name="blob">The blob sha.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The bytes, NotFound (SND-CONTEXT-003) when the repo has no such blob, or SND-CONTEXT-010.</returns>
    public async ValueTask<Result<byte[]>> BlobBytes(string repo, string blob, CancellationToken cancellationToken)
    {
        var exists = await Exists(repo, blob, cancellationToken).ConfigureAwait(false);
        var run = exists.IsError ? exists.Error : await RunBytes(repo, ["cat-file", "blob", blob], cancellationToken).ConfigureAwait(false);
        return run.Bind<byte[]>(r => r.Exit == 0 ? r.Stdout : Failed("cat-file", r.Exit));
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
        var start = CleanGit(["-C", repo, "-c", "core.quotepath=off", .. args]);
        if (locale is not null)
        {
            start.Environment["LC_ALL"] = locale;   // case folding of 'grep -i' follows the locale
        }

        return await Execute(start, args[0], static (stdout, token) => stdout.ReadToEndAsync(token), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Like <see cref="Run"/> but stdout is git's bytes, copied from the raw stream: no StreamReader, so no BOM sniffing or decoding.</summary>
    private async ValueTask<Result<(int Exit, byte[] Stdout)>> RunBytes(string repo, string[] args, CancellationToken cancellationToken)
    {
        var start = CleanGit(["-C", repo, "-c", "core.quotepath=off", .. args]);
        return await Execute(start, args[0], CopyBytes, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<byte[]> CopyBytes(StreamReader stdout, CancellationToken token)
    {
        using var copy = new MemoryStream();
        await stdout.BaseStream.CopyToAsync(copy, token).ConfigureAwait(false);
        return copy.ToArray();
    }

    private async ValueTask<Result<(int Exit, T Stdout)>> Execute<T>(ProcessStartInfo start, string verb, Func<StreamReader, CancellationToken, Task<T>> readStdout, CancellationToken cancellationToken,
        Func<StreamWriter, CancellationToken, Task>? writeStdin = null)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);
        Process? git = null;
        try
        {
            git = Process.Start(start) ?? throw new InvalidOperationException("git didn't start");
            var stdout = readStdout(git.StandardOutput, timeout.Token);
            var stderr = git.StandardError.ReadToEndAsync(timeout.Token);
            var stdin = writeStdin is null ? Task.CompletedTask : Feed(git.StandardInput, writeStdin, timeout.Token);   // written while stdout is read, so neither pipe fills
            await git.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            await stdin.ConfigureAwait(false);
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

    private static async Task Feed(StreamWriter stdin, Func<StreamWriter, CancellationToken, Task> write, CancellationToken token)
    {
        try
        {
            await write(stdin, token).ConfigureAwait(false);
            await stdin.FlushAsync(token).ConfigureAwait(false);
            stdin.Close();
        }
        catch (IOException)
        {
            // git exited before it read everything: its exit code says why
        }
    }

    private ProcessStartInfo CleanGit(string[] args)
    {
        var start = new ProcessStartInfo(_command[0], [.. _command.Skip(1), .. args]) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, StandardOutputEncoding = Encoding.UTF8 };
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
