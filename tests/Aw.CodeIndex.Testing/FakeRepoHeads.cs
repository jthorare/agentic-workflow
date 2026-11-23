using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Testing;

/// <summary>Heads set by the test (spec §11: the context lane owns the real contract; this is the settable stand-in until it lands).</summary>
public sealed class FakeRepoHeads : IRepoHeads
{
    private readonly Dictionary<RepoName, RepoHead> _heads = [];
    private readonly Dictionary<(RepoName, CommitSha), DefaultBranchCheck> _checks = [];

    /// <summary>Sets a repo's head.</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="head">The checkout's HEAD, or null when unreadable.</param>
    /// <param name="onDefaultBranch">Whether it is on the default branch.</param>
    /// <param name="defaultTip">The default branch's tip, or null when the repo has no main and no origin/main.</param>
    /// <returns>This fake.</returns>
    public FakeRepoHeads With(RepoName repo, CommitSha? head, bool onDefaultBranch, CommitSha? defaultTip = null)
    {
        _heads[repo] = new RepoHead(repo, head, onDefaultBranch, defaultTip);
        return this;
    }

    /// <summary>Sets whether a commit counts as on the default branch (without a call, every known commit is on 'main').</summary>
    /// <param name="repo">The repo.</param>
    /// <param name="commit">The commit.</param>
    /// <param name="contains">Whether the default branch contains it.</param>
    /// <returns>This fake.</returns>
    public FakeRepoHeads WithCommit(RepoName repo, CommitSha commit, bool contains)
    {
        _checks[(repo, commit)] = new DefaultBranchCheck("main", contains);
        return this;
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<RepoHead>>> Heads(IReadOnlyList<CodeIndexRepo> repos, CancellationToken cancellationToken)
        => ValueTask.FromResult<Result<IReadOnlyList<RepoHead>>>(repos
            .Select(r => _heads.TryGetValue(r.Name, out var head) ? head : new RepoHead(r.Name, Head: null, OnDefaultBranch: false))
            .ToList());

    /// <inheritdoc />
    public ValueTask<Result<DefaultBranchCheck>> OnDefaultBranch(CodeIndexRepo repo, CommitSha commit, CancellationToken cancellationToken)
        => ValueTask.FromResult<Result<DefaultBranchCheck>>(_checks.GetValueOrDefault((repo.Name, commit), new DefaultBranchCheck("main", Contains: true)));
}
