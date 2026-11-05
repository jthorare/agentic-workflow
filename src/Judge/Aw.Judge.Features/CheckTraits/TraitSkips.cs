using Aw.Judge.Core.Ports;

namespace Aw.Judge.Features.CheckTraits;

/// <summary>
/// What a check never judges: files .gitattributes marks linguist-generated (snapshots, lockfiles, migration
/// metadata), and, for edit and stop, a working tree in the middle of a merge, rebase, cherry-pick or revert, whose
/// conflicted files hold code the agent is reconciling, not writing. A git call that fails skips nothing.
/// </summary>
/// <param name="git">git through the process runner.</param>
/// <param name="files">The repo's files.</param>
internal sealed class TraitSkips(TraitGit git, ITraitFiles files)
{
    // The files git leaves while an operation waits on the user; any one of them means the operation is unfinished.
    private static readonly string[] _inProgressMarkers = ["MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "rebase-merge/head-name", "rebase-apply/next"];

    internal async ValueTask<bool> OperationInProgress(string root, CancellationToken cancellationToken)
    {
        foreach (var marker in _inProgressMarkers)
        {
            var (code, path) = await git.Run(["rev-parse", "--path-format=absolute", "--git-path", marker], root, cancellationToken).ConfigureAwait(false);
            if (code == 0 && files.FileSize(path.Trim()) is not null)
            {
                return true;
            }
        }

        return false;
    }

    // The paths go on stdin (a scan can list the whole repo); 'git check-attr -z' prints path, attribute and value, each
    // NUL-terminated, and a set attribute is "true" or "set".
    internal async ValueTask<HashSet<string>> Generated(string root, IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        HashSet<string> generated = new(StringComparer.Ordinal);
        if (paths.Count == 0)
        {
            return generated;
        }

        var (code, output) = await git.Run(["check-attr", "-z", "--stdin", "linguist-generated"], root, cancellationToken, string.Join('\0', paths) + "\0").ConfigureAwait(false);
        var fields = code == 0 ? output.Split('\0') : [];
        for (var i = 0; i + 2 < fields.Length; i += 3)
        {
            if (string.Equals(fields[i + 2], "true", StringComparison.Ordinal) || string.Equals(fields[i + 2], "set", StringComparison.Ordinal))
            {
                _ = generated.Add(fields[i]);
            }
        }

        return generated;
    }
}
