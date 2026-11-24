using System.Collections.Concurrent;
using Aw.Transcripts.Core;

namespace Aw.Transcripts.Infrastructure.Repos;

/// <summary>
/// Maps a session's cwd to the main repo it belongs to: the nearest directory with a '.git'. A '.git' file is a
/// worktree's (or a submodule's) pointer, 'gitdir: &lt;dir&gt;'; that dir's 'commondir' names the main repo's git dir,
/// whose parent is the main checkout. A cwd that no longer exists, or is in no repo, is kept as it is. A cwd that is no
/// valid path (a NUL byte) has no repo: null, never an exception. A cwd that no longer exists and sits under one of the
/// host's worktree roots belongs to that root's main checkout.
/// </summary>
/// <param name="worktreeRoots">The host's (worktree root, main checkout) pairs; empty keeps a gone cwd as it is.</param>
internal sealed class RepoResolver(IReadOnlyList<WorktreeRoot> worktreeRoots)
{
    private readonly ConcurrentDictionary<string, string?> _known = new(StringComparer.Ordinal);

    public string? Resolve(string cwd) => _known.GetOrAdd(cwd, Find);

    private string? Find(string cwd)
    {
        if (cwd.AsSpan().IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return null;
        }

        if (!Path.IsPathFullyQualified(cwd))
        {
            return cwd;
        }

        if (!Directory.Exists(cwd) && !File.Exists(cwd))
        {
            return WorktreeRoots.MainCheckoutOf(worktreeRoots, cwd) ?? cwd;
        }

        try
        {
            for (var dir = new DirectoryInfo(Path.TrimEndingDirectorySeparator(cwd)); dir is { Exists: true }; dir = dir.Parent)
            {
                var git = Path.Combine(dir.FullName, ".git");
                if (Directory.Exists(git))
                {
                    return dir.FullName;
                }

                if (File.Exists(git))
                {
                    return MainCheckout(dir.FullName, git) ?? dir.FullName;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return cwd;
        }

        return cwd;
    }

    // The main checkout behind a '.git' pointer file, or null when it doesn't point at a worktree's git dir.
    private static string? MainCheckout(string checkout, string pointer)
    {
        var first = File.ReadLines(pointer).FirstOrDefault() ?? "";
        const string Prefix = "gitdir:";
        if (!first.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var gitDir = Path.GetFullPath(first[Prefix.Length..].Trim(), checkout);
        var commonFile = Path.Combine(gitDir, "commondir");
        if (!File.Exists(commonFile))
        {
            return null;
        }

        var common = Path.TrimEndingDirectorySeparator(Path.GetFullPath(File.ReadLines(commonFile).FirstOrDefault()?.Trim() ?? "", gitDir));
        return string.Equals(Path.GetFileName(common), ".git", StringComparison.Ordinal) ? Path.GetDirectoryName(common) : common;
    }
}
