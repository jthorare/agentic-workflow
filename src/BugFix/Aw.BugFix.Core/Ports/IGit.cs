using Aw.BugFix.Core.Model;
using Aw.Kernel;

namespace Aw.BugFix.Core.Ports;

/// <summary>The git questions the bug-fix gates ask. Every failure is AW-BUGFIX-018 unless stated.</summary>
public interface IGit
{
    /// <summary>The commit HEAD points at in <paramref name="directory"/>.</summary>
    /// <param name="directory">A directory inside a work tree.</param>
    /// <returns>The full commit sha.</returns>
    public Result<string> Head(string directory);

    /// <summary>The branch checked out in <paramref name="directory"/> ("HEAD" when detached).</summary>
    /// <param name="directory">A directory inside a work tree.</param>
    /// <returns>The branch name.</returns>
    public Result<string> Branch(string directory);

    /// <summary>Whether the work tree has no uncommitted change to a tracked file (untracked files are <see cref="Untracked"/>).</summary>
    /// <param name="directory">A directory inside a work tree.</param>
    /// <returns>True when clean.</returns>
    public Result<bool> IsClean(string directory);

    /// <summary>The untracked files of the whole work tree, not ignored ones, repository-relative with '/'.</summary>
    /// <param name="directory">A directory inside a work tree.</param>
    /// <returns>The paths.</returns>
    public Result<IReadOnlyList<string>> Untracked(string directory);

    /// <summary>The repository root (real path) of <paramref name="directory"/>.</summary>
    /// <param name="directory">A directory inside a work tree.</param>
    /// <returns>The root.</returns>
    public Result<string> Root(string directory);

    /// <summary>Where <paramref name="directory"/> is inside its work tree, relative to the root, with '/' ("" for the root).</summary>
    /// <param name="directory">A directory inside a work tree.</param>
    /// <returns>The relative directory.</returns>
    public Result<string> RelativeDirectory(string directory);

    /// <summary>Whether <paramref name="file"/> (a real path inside the repository) is committed at HEAD with no working change.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="file">The repository-relative path, with '/'.</param>
    /// <returns>True when committed and unchanged.</returns>
    public Result<bool> IsCommittedUnchanged(string root, string file);

    /// <summary>Whether <paramref name="ancestor"/> is an ancestor of (or equal to) <paramref name="commit"/>.</summary>
    /// <param name="directory">A directory inside the repository.</param>
    /// <param name="ancestor">The possible ancestor.</param>
    /// <param name="commit">The commit.</param>
    /// <returns>True when it is.</returns>
    public Result<bool> IsAncestor(string directory, string ancestor, string commit);

    /// <summary>Whether two commits have the same tree.</summary>
    /// <param name="directory">A directory inside the repository.</param>
    /// <param name="first">One commit.</param>
    /// <param name="second">The other commit.</param>
    /// <returns>True when the trees are identical.</returns>
    public Result<bool> SameTree(string directory, string first, string second);

    /// <summary>
    /// The tree-ish a candidate is diffed against: the baseline plus whatever main added to the candidate's branch by a merge, so a
    /// merge of main after the baseline doesn't count as the fix's change. It is the baseline itself when main can't be found, when
    /// the merge can't be replayed cleanly, or on any git failure.
    /// </summary>
    /// <param name="directory">A directory inside the repository.</param>
    /// <param name="baseline">The baseline commit.</param>
    /// <param name="candidate">The candidate commit.</param>
    /// <returns>The "from" tree-ish.</returns>
    public string NetBase(string directory, string baseline, string candidate);

    /// <summary>The files changed between two commits, renames as delete plus add, paths raw with '/'.</summary>
    /// <param name="directory">A directory inside the repository.</param>
    /// <param name="from">The base commit.</param>
    /// <param name="to">The candidate commit.</param>
    /// <returns>The changed paths.</returns>
    public Result<IReadOnlyList<string>> ChangedFiles(string directory, string from, string to);

    /// <summary>The files added between two commits (not there at <paramref name="from"/>), paths raw with '/'.</summary>
    /// <param name="directory">A directory inside the repository.</param>
    /// <param name="from">The base commit.</param>
    /// <param name="to">The candidate commit.</param>
    /// <returns>The added paths.</returns>
    public Result<IReadOnlyList<string>> AddedFiles(string directory, string from, string to);

    /// <summary>The change between two commits, as git diff --stat and as the full diff without lock files and build output.</summary>
    /// <param name="directory">A directory inside the repository.</param>
    /// <param name="from">The base commit.</param>
    /// <param name="to">The candidate commit.</param>
    /// <returns>The diff stat and the diff (null when git can't produce the full diff).</returns>
    public Result<CommitDiff> Diff(string directory, string from, string to);

    /// <summary>
    /// The commits main has that the baseline lacks, up to the point the candidate's branch last met main (the commits
    /// <see cref="NetBase"/> adds to the baseline), oldest first, merges left out. Empty when main can't be found.
    /// </summary>
    /// <param name="directory">A directory inside the repository.</param>
    /// <param name="baseline">The baseline commit.</param>
    /// <param name="candidate">The candidate commit.</param>
    /// <returns>The commits, each with its parent and full message.</returns>
    public Result<IReadOnlyList<MainCommit>> MainCommits(string directory, string baseline, string candidate);

    /// <summary>The text of <paramref name="path"/> as committed at <paramref name="commit"/>.</summary>
    /// <param name="directory">A directory inside the repository.</param>
    /// <param name="commit">The commit.</param>
    /// <param name="path">The repository-relative path, with '/'.</param>
    /// <returns>The file's text, or a git failure when the commit has no such file.</returns>
    public Result<string> FileAt(string directory, string commit, string path);
}
