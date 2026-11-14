using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Infrastructure.Sessions;

/// <summary>
/// The branch of a worktree from its files, with no git process: it walks up from the directory to the folder that holds '.git'; a '.git' file ('gitdir: &lt;path&gt;', a linked worktree) points at the
/// folder that holds HEAD, a '.git' folder holds it itself. 'ref: refs/heads/&lt;name&gt;' is the branch; a commit id (detached HEAD) or anything else is null.
/// </summary>
internal sealed class GitHeadBranches : IBranchReader
{
    private const string HeadsPrefix = "ref: refs/heads/";
    private const string GitDirPrefix = "gitdir: ";
    private const int MaxLevels = 64;

    public string? Branch(string directory)
    {
        ArgumentNullException.ThrowIfNull(directory);
        try
        {
            return HeadFile(directory) is { } head && File.Exists(head) ? Parse(File.ReadLines(head).FirstOrDefault()) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return null;   // a row never fails because a worktree can't be read
        }
    }

    private static string? Parse(string? line) =>
        line is not null && line.StartsWith(HeadsPrefix, StringComparison.Ordinal) && line[HeadsPrefix.Length..].Trim() is { Length: > 0 } name ? name : null;

    private static string? HeadFile(string directory)
    {
        var current = directory;
        for (var level = 0; level < MaxLevels && !string.IsNullOrEmpty(current); level++)
        {
            var git = Path.Combine(current, ".git");
            if (Directory.Exists(git))
            {
                return Path.Combine(git, "HEAD");
            }

            if (File.Exists(git))
            {
                return File.ReadLines(git).FirstOrDefault() is { } first && first.StartsWith(GitDirPrefix, StringComparison.Ordinal)
                    ? Path.Combine(Path.GetFullPath(first[GitDirPrefix.Length..].Trim(), current), "HEAD")
                    : null;
            }

            current = Path.GetDirectoryName(current);
        }

        return null;
    }
}
