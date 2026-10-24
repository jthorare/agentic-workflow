using System.Diagnostics;
using System.Globalization;
using Aw.CodeIndex.Core;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Infrastructure.Snapshot;

/// <summary>The repo's tree at a commit, read with git only (spec §5.1).</summary>
internal sealed class GitRepoSnapshot : IRepoSnapshot
{
    private static readonly TimeSpan _listTimeout = TimeSpan.FromMinutes(10);

    /// <inheritdoc />
    public async ValueTask<Result<SnapshotInventory>> Inventory(CodeIndexRepo repo, CommitSha commit, SnapshotLimits limits, CancellationToken cancellationToken)
    {
        var config = await Run(repo.Path, cancellationToken, "config", "--list").ConfigureAwait(false);
        if (config.IsError)
        {
            return config.Error;
        }

        if (!IsPartialClone(config.Value))
        {
            var listed = await Run(repo.Path, cancellationToken, "ls-tree", "-r", "-z", "--full-tree", "--long", commit.Value).ConfigureAwait(false);
            return listed.IsError ? listed.Error : InputRules.Apply(commit, Parse(listed.Value, presentSizes: null), limits);
        }

        // A partial clone: asking git for a missing blob's size dies on git 2.43 even with lazy fetch off, so the tree is
        // listed without sizes and the sizes come from the objects the clone holds; a blob it lacks has none and is 'missing'.
        var tree = await Run(repo.Path, cancellationToken, "ls-tree", "-r", "-z", "--full-tree", commit.Value).ConfigureAwait(false);
        if (tree.IsError)
        {
            return tree.Error;
        }

        var present = await Run(repo.Path, cancellationToken, "cat-file", "--batch-all-objects", "--unordered", "--batch-check=%(objectname) %(objectsize)").ConfigureAwait(false);
        return present.IsError ? present.Error : InputRules.Apply(commit, Parse(tree.Value, Sizes(present.Value)), limits);
    }

    /// <inheritdoc />
    public async ValueTask<Result<IBlobReader>> OpenBlobs(CodeIndexRepo repo, CancellationToken cancellationToken)
    {
        var check = await Run(repo.Path, cancellationToken, "rev-parse", "--git-dir").ConfigureAwait(false);
        return check.IsError ? check.Error : GitBlobReader.Start(repo.Path);
    }

    private static async ValueTask<Result<string>> Run(string repo, CancellationToken cancellationToken, params string[] args)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_listTimeout);
        Process? git = null;
        try
        {
            git = Process.Start(CleanGit.Prepare(repo, redirectInput: false, args));
            if (git is null)
            {
                return CodeIndexErrors.RepoNotFound.With("git did not start");
            }

            var stdout = ReadAll(git.StandardOutput, timeout.Token);
            var stderr = git.StandardError.BaseStream.CopyToAsync(Stream.Null, timeout.Token);
            await git.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
            var text = await stdout.ConfigureAwait(false);
            await stderr.ConfigureAwait(false);
            return git.ExitCode == 0
                ? text
                : CodeIndexErrors.RepoNotFound.With(string.Create(CultureInfo.InvariantCulture, $"git {args[0]} exited {git.ExitCode}"));
        }
        catch (OperationCanceledException)
        {
            Kill(git);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }

            return CodeIndexErrors.RepoNotFound.With($"git {args[0]} did not finish in time");
        }
        finally
        {
            git?.Dispose();
        }
    }

    private static async Task<string> ReadAll(StreamReader reader, CancellationToken cancellationToken) =>
        await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

    private static void Kill(Process? git)
    {
        try
        {
            if (git is { HasExited: false })
            {
                git.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // it exited between the check and the kill
        }
    }

    private static bool IsPartialClone(string config) =>
        config.Split('\n').Any(line =>
            line.StartsWith("extensions.partialclone=", StringComparison.OrdinalIgnoreCase)
            || (line.StartsWith("remote.", StringComparison.OrdinalIgnoreCase) && line.EndsWith(".promisor=true", StringComparison.OrdinalIgnoreCase)));

    private static Dictionary<string, long> Sizes(string batchCheck)
    {
        var sizes = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var line in batchCheck.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var space = line.IndexOf(' ', StringComparison.Ordinal);
            if (space > 0 && long.TryParse(line.AsSpan(space + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var bytes))
            {
                sizes[line[..space]] = bytes;
            }
        }

        return sizes;
    }

    private static IEnumerable<TreeEntry> Parse(string output, Dictionary<string, long>? presentSizes)
    {
        foreach (var record in output.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            // "<mode> SP <type> SP <object> SP <size, padded> TAB <path>", or without the size when presentSizes is given
            var tab = record.IndexOf('\t', StringComparison.Ordinal);
            if (tab < 0)
            {
                continue;
            }

            var meta = record[..tab].Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (meta.Length != (presentSizes is null ? 4 : 3) || meta[1] == "tree")
            {
                continue;
            }

            long? size = presentSizes is null
                ? long.TryParse(meta[3], NumberStyles.None, CultureInfo.InvariantCulture, out var bytes) ? bytes : null
                : presentSizes.TryGetValue(meta[2], out var held) ? held : null;
            yield return new TreeEntry(meta[0], record[(tab + 1)..], meta[2], meta[1] == "blob" ? size : 0);
        }
    }
}
