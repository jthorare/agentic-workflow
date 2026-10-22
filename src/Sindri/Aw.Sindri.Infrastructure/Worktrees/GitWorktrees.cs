using System.Collections.Concurrent;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Worktrees;
using Aw.Sindri.Infrastructure.Workflow;

namespace Aw.Sindri.Infrastructure.Worktrees;

/// <summary>
/// Worktrees through git ('git worktree add' and 'remove'). It never fetches, forces or resets: a branch that exists at the
/// base is reused, one at another commit is a conflict; a dirty tree is refused, not discarded. Create is idempotent through
/// 'git worktree list --porcelain': the path is already a worktree of that branch with HEAD at the base. Each git call is
/// bounded to 30 s.
/// </summary>
internal sealed class GitWorktrees(IAgentTrust? trust = null) : IWorktrees
{
    private const string HeadsPrefix = "refs/heads/";

    private readonly ConcurrentDictionary<string, string> _repos = new(StringComparer.Ordinal);   // path -> repo of the worktrees made here, to prune after the directory is gone

    public async ValueTask<Result<WorktreeCheckout>> Create(WorktreeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Validate() is { } invalid)
        {
            return invalid;
        }

        var commit = await GitProcess.Run(request.RepoRoot, ["rev-parse", "--verify", "--quiet", request.BaseCommit + "^{commit}"], cancellationToken).ConfigureAwait(false);
        if (commit.Exit != 0 || commit.Output != request.BaseCommit)
        {
            return SindriErrors.WorktreeGitFailed.With($"{request.BaseCommit} is not a commit of {request.RepoRoot}: {Reason(commit)}");
        }

        // Entries whose directory is gone (deleted by hand, a lost mount) are dropped first, so none is answered as a checkout. Prune touches only such entries.
        var pruned = await GitProcess.Run(request.RepoRoot, ["worktree", "prune"], cancellationToken).ConfigureAwait(false);
        if (pruned.Exit != 0)
        {
            return SindriErrors.WorktreeGitFailed.With($"git worktree prune failed in {request.RepoRoot}: {Reason(pruned)}");
        }

        var plan = await Plan(request, cancellationToken).ConfigureAwait(false);
        if (plan.Answer is { } answer)
        {
            return answer;
        }

        string[] add = plan.BranchExists
            ? ["worktree", "add", request.Path, request.Branch]
            : ["worktree", "add", "-b", request.Branch, request.Path, request.BaseCommit];
        var made = await GitProcess.Run(request.RepoRoot, add, cancellationToken).ConfigureAwait(false);
        if (made.Exit == 0)
        {
            _repos[Normal(request.Path)] = request.RepoRoot;
            return new WorktreeCheckout(request.Path, request.BaseCommit, request.Branch);
        }

        // A retry racing its predecessor: look again, and answer what is there now (the checkout, or why it can't be) before calling it a git failure.
        var again = await Plan(request, cancellationToken).ConfigureAwait(false);
        return again.Answer ?? SindriErrors.WorktreeGitFailed.With($"git worktree add {request.Path} failed in {request.RepoRoot}: {Reason(made)}");
    }

    public async ValueTask<Result<Unit>> Remove(string path, CancellationToken cancellationToken)
    {
        var removed = await RemoveWorktree(path, cancellationToken).ConfigureAwait(false);
        if (removed.IsSuccess && trust is not null)
        {
            _ = await trust.Untrust(path, cancellationToken).ConfigureAwait(false);   // the agent CLI's trust entry goes with the worktree; a file that can't be edited is left, the worktree is gone either way
        }

        return removed;
    }

    private async ValueTask<Result<Unit>> RemoveWorktree(string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (!Path.IsPathRooted(path))
        {
            return SindriErrors.WorktreeInvalid.With("Path must be an absolute path");
        }

        if (!Directory.Exists(path))
        {
            return await PruneGone(path, cancellationToken).ConfigureAwait(false);
        }

        var (repository, problem) = await Inspect(path, cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            return problem;
        }

        var removed = await GitProcess.Run(repository, ["worktree", "remove", path], cancellationToken).ConfigureAwait(false);
        if (removed.Exit != 0)
        {
            return removed.Error.Contains("modified or untracked", StringComparison.Ordinal)
                ? Dirty(path)
                : SindriErrors.WorktreeGitFailed.With($"git worktree remove {path} failed: {Reason(removed)}");
        }

        _ = _repos.TryRemove(Normal(path), out _);
        var pruned = await GitProcess.Run(repository, ["worktree", "prune"], cancellationToken).ConfigureAwait(false);
        return pruned.Exit == 0
            ? Results.Ok()
            : SindriErrors.WorktreeGitFailed.With($"git worktree prune failed in {repository}: {Reason(pruned)}");
    }

    /// <summary>
    /// A path that is already gone is a success, and its stale registration is pruned when the repo can be found: the one this
    /// adapter made it in, else the repo of the nearest existing parent directory (a worktree under '&lt;repo&gt;/.claude/worktrees').
    /// Remove has no repo argument, so a gone path that is neither is left for the next Create's prune.
    /// </summary>
    private async Task<Result<Unit>> PruneGone(string path, CancellationToken cancellationToken)
    {
        var repository = _repos.TryRemove(Normal(path), out var known) ? known : null;
        for (var parent = Path.GetDirectoryName(path.TrimEnd('/')); repository is null && parent is not null; parent = Path.GetDirectoryName(parent))
        {
            if (Directory.Exists(parent))
            {
                repository = await MainCheckout(parent, cancellationToken).ConfigureAwait(false);
                break;
            }
        }

        if (repository is null)
        {
            return Results.Ok();
        }

        var pruned = await GitProcess.Run(repository, ["worktree", "prune"], cancellationToken).ConfigureAwait(false);
        return pruned.Exit == 0
            ? Results.Ok()
            : SindriErrors.WorktreeGitFailed.With($"git worktree prune failed in {repository}: {Reason(pruned)}");
    }

    /// <summary>The main checkout of the repo that <paramref name="directory"/> is in, or null outside git.</summary>
    private static async Task<string?> MainCheckout(string directory, CancellationToken cancellationToken)
    {
        var common = await GitProcess.Run(directory, ["rev-parse", "--path-format=absolute", "--git-common-dir"], cancellationToken).ConfigureAwait(false);
        return common.Exit == 0 && common.Output.Length > 0 ? Path.GetDirectoryName(common.Output.TrimEnd('/')) : null;
    }

    private sealed record Planned(Result<WorktreeCheckout>? Answer, bool BranchExists);

    /// <summary>Looks at the repo's worktrees and the branch and says what Create does: an answer (the checkout, or why not), or null to go and add.</summary>
    private static async Task<Planned> Plan(WorktreeRequest request, CancellationToken cancellationToken)
    {
        var listed = await GitProcess.Run(request.RepoRoot, ["worktree", "list", "--porcelain"], cancellationToken).ConfigureAwait(false);
        var main = listed.Exit == 0 ? await MainCheckout(request.RepoRoot, cancellationToken).ConfigureAwait(false) : null;
        if (main is null)
        {
            return new Planned(SindriErrors.WorktreeGitFailed.With($"the worktrees of {request.RepoRoot} can't be listed: {Reason(listed)}"), BranchExists: false);
        }

        var tip = await GitProcess.Run(request.RepoRoot, ["rev-parse", "--verify", "--quiet", HeadsPrefix + request.Branch], cancellationToken).ConfigureAwait(false);
        if (tip.Exit > 1)
        {
            return new Planned(SindriErrors.WorktreeGitFailed.With($"the branch {request.Branch} can't be looked up in {request.RepoRoot}: {Reason(tip)}"), BranchExists: false);
        }

        var worktrees = Parse(listed.Output);
        var branchExists = tip.Exit == 0;
        if (Existing(worktrees, request) is { } existing)
        {
            return new Planned(existing, branchExists);
        }

        var conflict = Conflict(worktrees, request, Normal(main), branchExists ? tip.Output : null);
        return new Planned(conflict is null ? null : Results.Failure<WorktreeCheckout>(conflict), branchExists);
    }

    /// <summary>The main checkout a worktree belongs to, or why it can't be removed: not a worktree, unreadable, or dirty.</summary>
    private static async Task<(string Repository, Error? Problem)> Inspect(string path, CancellationToken cancellationToken)
    {
        var common = await GitProcess.Run(path, ["rev-parse", "--path-format=absolute", "--git-common-dir"], cancellationToken).ConfigureAwait(false);
        var changes = common.Exit == 0 && common.Output.Length > 0
            ? await GitProcess.Run(path, ["--no-optional-locks", "status", "--porcelain"], cancellationToken).ConfigureAwait(false)
            : null;
        if (changes is not { Exit: 0 })
        {
            return (string.Empty, SindriErrors.WorktreeGitFailed.With($"{path} is not a git worktree, or its status can't be read: {Reason(changes ?? common)}"));
        }

        return (Path.GetDirectoryName(common.Output.TrimEnd('/')) ?? "/", changes.Output.Length > 0 ? Dirty(path) : null);
    }

    private static Error Dirty(string path) => SindriErrors.WorktreeDirty.With($"{path} has uncommitted or untracked files; it was left as it is");

    /// <summary>The worktree already at the request's path: the checkout when it is the one asked for, a conflict when it is not, null when the path is free.</summary>
    private static Result<WorktreeCheckout>? Existing(List<Listed> worktrees, WorktreeRequest request)
    {
        var path = Normal(request.Path);
        if (worktrees.FirstOrDefault(w => Normal(w.Path) == path) is not { } found)
        {
            return null;
        }

        if (!Directory.Exists(found.Path))
        {
            return SindriErrors.WorktreeGitFailed.With($"{request.Path} is listed as a worktree but its directory is gone and can't be pruned (is it locked?)");
        }

        return found.Branch == HeadsPrefix + request.Branch && found.Head == request.BaseCommit
            ? new WorktreeCheckout(request.Path, found.Head, request.Branch)
            : SindriErrors.WorktreeConflict.With($"{request.Path} is already a worktree of {found.Branch ?? "a detached HEAD"} at {found.Head}, not of {request.Branch} at {request.BaseCommit}");
    }

    /// <summary>Why a free path can't be used: it is inside another linked worktree, or the branch is checked out elsewhere or sits at another commit (nothing is reset). Null when it can be used.</summary>
    private static Error? Conflict(List<Listed> worktrees, WorktreeRequest request, string main, string? branchTip)
    {
        var path = Normal(request.Path);
        // The main checkout (found through the common git dir, so a RepoRoot that is a linked worktree works too) may hold the new one
        // (<repo>/.claude/worktrees/…); another linked worktree may not.
        if (worktrees.FirstOrDefault(w => Normal(w.Path) != main && path.StartsWith(Normal(w.Path) + "/", StringComparison.Ordinal)) is { } outer)
        {
            return SindriErrors.WorktreeInvalid.With($"{request.Path} is inside the worktree {outer.Path}");
        }

        if (worktrees.FirstOrDefault(w => w.Branch == HeadsPrefix + request.Branch) is { } holder)
        {
            return SindriErrors.WorktreeConflict.With($"{request.Branch} is checked out in {holder.Path}");
        }

        return branchTip is not null && branchTip != request.BaseCommit
            ? SindriErrors.WorktreeConflict.With($"{request.Branch} exists at {branchTip}, not at {request.BaseCommit}; it is not reset")
            : null;
    }

    private static string Reason(GitRun run) => run.Error.Trim().Length > 0 ? run.Error.Trim() : "exit " + run.Exit.ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Normal(string path) => (RealPath.Of(path) ?? path).TrimEnd('/');

    /// <summary>The entries of 'git worktree list --porcelain': blocks of 'worktree', 'HEAD' and 'branch' (or 'detached') lines separated by blank lines.</summary>
    private static List<Listed> Parse(string porcelain)
    {
        var entries = new List<Listed>();
        foreach (var block in porcelain.Split("\n\n", StringSplitOptions.RemoveEmptyEntries))
        {
            string? path = null, head = null, branch = null;
            foreach (var line in block.Split('\n'))
            {
                if (line.StartsWith("worktree ", StringComparison.Ordinal))
                {
                    path = line["worktree ".Length..];
                }
                else if (line.StartsWith("HEAD ", StringComparison.Ordinal))
                {
                    head = line["HEAD ".Length..];
                }
                else if (line.StartsWith("branch ", StringComparison.Ordinal))
                {
                    branch = line["branch ".Length..];
                }
            }

            if (path is not null)
            {
                entries.Add(new Listed(path, head ?? string.Empty, branch));
            }
        }

        return entries;
    }

    private sealed record Listed(string Path, string Head, string? Branch);
}
