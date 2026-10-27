using Aw.BugFix.Core.Errors;
using Aw.BugFix.Core.Model;
using Aw.BugFix.Core.Ports;
using Aw.Kernel;

namespace Aw.BugFix.Core.Rules;

/// <summary>
/// The frozen-check rule for a run: a fix must not weaken the check, neither the file nor what the run executed. A check file
/// that differs from the baseline only because main changed it (a shared project file, say) and the candidate merged main is
/// not the fix's change, so it is accepted with the reason.
/// </summary>
/// <param name="git">The git port.</param>
/// <param name="files">The file-system port.</param>
/// <param name="tree">The work-tree rules.</param>
public sealed class CheckPin(IGit git, IFiles files, WorkTree tree)
{
    /// <summary>Checks that <paramref name="record"/> ran the frozen check file as the baseline had it, or as main alone changed it.</summary>
    /// <param name="check">The frozen check.</param>
    /// <param name="baseline">The baseline commit.</param>
    /// <param name="candidate">The candidate the run belongs to.</param>
    /// <param name="record">The run.</param>
    /// <returns>The reason the check file was accepted although it changed ("" when it did not change), or AW-BUGFIX-008.</returns>
    public Result<string> Verify(FrozenCheck check, string baseline, Candidate candidate, RunRecord record)
    {
        var current = tree.Snapshot(candidate.Worktree).Bind(worktree => files.Sha256(files.FullPath(worktree.Root, check.Path)));
        if (current.IsError && current.Error.Code != BugFixErrors.FileUnreadable.Code)
        {
            return current.Error;
        }

        if (current.IsSuccess && current.Value == check.Sha256 && record.CheckSha256 == check.Sha256)
        {
            return string.Empty;
        }

        return current.IsSuccess && current.Value == record.CheckSha256 && ChangedOnlyByMain(check, baseline, candidate)
            ? $"; the check file {check.Path} changed on main since the baseline, not in the fix, and the run executed it as it is in the candidate"
            : BugFixErrors.CheckNotFrozen.With($"the check file {check.Path} changed since the baseline");
    }

    // The candidate's own changes are measured from the net base (the baseline plus what main added by a merge).
    private bool ChangedOnlyByMain(FrozenCheck check, string baseline, Candidate candidate)
    {
        var net = git.NetBase(candidate.Worktree, baseline, candidate.Commit);
        var changed = git.ChangedFiles(candidate.Worktree, net, candidate.Commit);
        return changed.IsSuccess && !changed.Value.Contains(check.Path, StringComparer.Ordinal);
    }
}
