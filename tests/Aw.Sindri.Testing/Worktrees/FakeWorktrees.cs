using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Worktrees;

namespace Aw.Sindri.Testing.Worktrees;

/// <summary>
/// The in-memory <see cref="IWorktrees"/>: repos with the commits they hold, and the worktrees made in them. Passes <see cref="WorktreesContract"/>.
/// <see cref="MakeDirty"/>, <see cref="AdvanceHead"/>, <see cref="Detach"/> and <see cref="DeleteDirectory"/> stand in for what happens in a checkout behind the
/// port's back. It cannot model a directory that exists on disk but is not a worktree (git refuses to add there, SND-WT-003; the fake succeeds), and it
/// compares paths as written, not resolved.
/// </summary>
public sealed class FakeWorktrees : IWorktrees
{
    private readonly Dictionary<string, HashSet<string>> _repos = [];
    private readonly Dictionary<(string Repo, string Branch), string> _branches = [];   // a branch's tip; it stays after its worktree goes, as in git
    private readonly Dictionary<string, (string Repo, WorktreeCheckout Checkout, bool Dirty, bool Detached, bool Gone)> _worktrees = [];
    private readonly List<WorktreeRequest> _requests = [];
    private readonly Lock _gate = new();

    /// <summary>Every request Create was given, in order.</summary>
    public IReadOnlyList<WorktreeRequest> Requests
    {
        get
        {
            lock (_gate)
            {
                return [.. _requests];
            }
        }
    }

    /// <summary>Tells the fake which commits a repo holds; a repo or sha it was not told about fails like git does.</summary>
    /// <param name="repoRoot">The repo's path.</param>
    /// <param name="commits">Its commit shas.</param>
    /// <returns>This fake.</returns>
    public FakeWorktrees WithRepo(string repoRoot, params string[] commits)
    {
        lock (_gate)
        {
            _repos[repoRoot] = [.. commits];
        }

        return this;
    }

    /// <summary>Tells the fake a branch of a repo that a worktree did not make, such as a parent phase's pushed head branch.</summary>
    /// <param name="repoRoot">The repo's path.</param>
    /// <param name="branch">The branch name.</param>
    /// <param name="commit">The commit it points at.</param>
    /// <returns>This fake.</returns>
    public FakeWorktrees WithBranch(string repoRoot, string branch, string commit)
    {
        lock (_gate)
        {
            _branches[(repoRoot, branch)] = commit;
        }

        return this;
    }

    /// <summary>Leaves an uncommitted file in the worktree at <paramref name="path"/>.</summary>
    /// <param name="path">The worktree's path.</param>
    public void MakeDirty(string path)
    {
        lock (_gate)
        {
            _worktrees[path] = _worktrees[path] with { Dirty = true };
        }
    }

    /// <summary>Commits in the worktree at <paramref name="path"/>: its HEAD (and its branch) move on to a new commit.</summary>
    /// <param name="path">The worktree's path.</param>
    public void AdvanceHead(string path)
    {
        lock (_gate)
        {
            var found = _worktrees[path];
            var head = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(found.Checkout.Head + "+")))[..40];
            _worktrees[path] = found with { Checkout = found.Checkout with { Head = head } };
            _branches[(found.Repo, found.Checkout.Branch)] = head;
        }
    }

    /// <summary>Detaches HEAD in the worktree at <paramref name="path"/>.</summary>
    /// <param name="path">The worktree's path.</param>
    public void Detach(string path)
    {
        lock (_gate)
        {
            _worktrees[path] = _worktrees[path] with { Detached = true };
        }
    }

    /// <summary>Deletes the worktree's directory behind the port's back: the registration stays until a prune.</summary>
    /// <param name="path">The worktree's path.</param>
    public void DeleteDirectory(string path)
    {
        lock (_gate)
        {
            _worktrees[path] = _worktrees[path] with { Gone = true };
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<WorktreeCheckout>> Create(WorktreeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_gate)
        {
            _requests.Add(request);
            return ValueTask.FromResult(Make(request));
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<string>> Resolve(string repoRoot, string branch, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repoRoot);
        ArgumentNullException.ThrowIfNull(branch);
        lock (_gate)
        {
            if (!Path.IsPathRooted(repoRoot) || string.IsNullOrWhiteSpace(branch) || branch.StartsWith('-') || branch.Any(c => char.IsWhiteSpace(c) || char.IsControl(c)))
            {
                return ValueTask.FromResult<Result<string>>(SindriErrors.WorktreeInvalid.With("RepoRoot must be absolute and Branch a name without spaces that does not start with '-'"));
            }

            return ValueTask.FromResult<Result<string>>(_repos.ContainsKey(repoRoot) && _branches.TryGetValue((repoRoot, branch), out var tip)
                ? tip
                : SindriErrors.WorktreeGitFailed.With($"the branch {branch} is not known in {repoRoot}"));
        }
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Remove(string path, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(path);
        lock (_gate)
        {
            if (!Path.IsPathRooted(path))
            {
                return ValueTask.FromResult<Result<Unit>>(SindriErrors.WorktreeInvalid.With("Path must be an absolute path"));
            }

            Prune();
            if (_worktrees.TryGetValue(path, out var found) && found.Dirty)
            {
                return ValueTask.FromResult<Result<Unit>>(SindriErrors.WorktreeDirty.With($"{path} has uncommitted changes"));
            }

            _ = _worktrees.Remove(path);
            return ValueTask.FromResult(Results.Ok());
        }
    }

    private Result<WorktreeCheckout> Make(WorktreeRequest request)
    {
        if (Unknown(request) is { } rejected)
        {
            return rejected;
        }

        Prune();

        if (_worktrees.TryGetValue(request.Path, out var existing))
        {
            return existing is { Detached: false } && existing.Repo == request.RepoRoot && existing.Checkout.Branch == request.Branch && existing.Checkout.Head == request.BaseCommit
                ? existing.Checkout
                : SindriErrors.WorktreeConflict.With($"{request.Path} is already a worktree of {(existing.Detached ? "a detached HEAD" : existing.Checkout.Branch)} at {existing.Checkout.Head}");
        }

        if (Unusable(request) is { } problem)
        {
            return problem;
        }

        _branches[(request.RepoRoot, request.Branch)] = request.BaseCommit;
        var checkout = new WorktreeCheckout(request.Path, request.BaseCommit, request.Branch);
        _worktrees[request.Path] = (request.RepoRoot, checkout, false, false, false);
        return checkout;
    }

    /// <summary>Drops the registrations whose directory is gone, as 'git worktree prune' does.</summary>
    private void Prune()
    {
        foreach (var gone in _worktrees.Where(w => w.Value.Gone).Select(w => w.Key).ToList())
        {
            _ = _worktrees.Remove(gone);
        }
    }

    private Error? Unknown(WorktreeRequest request)
    {
        if (request.Validate() is { } invalid)
        {
            return invalid;
        }

        return !_repos.TryGetValue(request.RepoRoot, out var commits) || !commits.Contains(request.BaseCommit)
            ? SindriErrors.WorktreeGitFailed.With($"{request.BaseCommit} is not a commit of {request.RepoRoot}")
            : null;
    }

    private Error? Unusable(WorktreeRequest request)
    {
        if (_worktrees.Keys.Any(other => request.Path.StartsWith(other + "/", StringComparison.Ordinal)))
        {
            return SindriErrors.WorktreeInvalid.With($"{request.Path} is inside another worktree");
        }

        if (_worktrees.Values.Any(w => w.Repo == request.RepoRoot && w.Checkout.Branch == request.Branch))
        {
            return SindriErrors.WorktreeConflict.With($"{request.Branch} is checked out in another worktree");
        }

        return _branches.TryGetValue((request.RepoRoot, request.Branch), out var tip) && tip != request.BaseCommit
            ? SindriErrors.WorktreeConflict.With($"{request.Branch} exists at {tip}, not at {request.BaseCommit}")
            : null;
    }
}
