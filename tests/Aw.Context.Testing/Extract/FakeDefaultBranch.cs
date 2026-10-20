using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Kernel;

namespace Aw.Context.Testing.Extract;

/// <summary>An in-memory default branch per repo: the commits on it are set by the test, and a repo it was told is unreadable answers SND-CONTEXT-048. A repo it knows nothing about has no default ref (false). <see cref="DefaultBranchContract"/> holds it to the git adapter's behavior.</summary>
public sealed class FakeDefaultBranch : IDefaultBranch
{
    private readonly Dictionary<string, HashSet<string>> _onDefault = new(StringComparer.Ordinal);
    private readonly HashSet<string> _unreadable = new(StringComparer.Ordinal);

    /// <summary>Sets the commits that are on a repo's default branch (replacing any set before).</summary>
    /// <param name="repo">The repo's name.</param>
    /// <param name="commits">The full shas the default branch contains.</param>
    /// <returns>This fake.</returns>
    public FakeDefaultBranch Reach(ContextRepoName repo, params string[] commits)
    {
        ArgumentNullException.ThrowIfNull(commits);
        _onDefault[repo.Value] = [.. commits];
        return this;
    }

    /// <summary>Makes a repo's checkout unreadable.</summary>
    /// <param name="repo">The repo's name.</param>
    /// <returns>This fake.</returns>
    public FakeDefaultBranch Unreadable(ContextRepoName repo)
    {
        _ = _unreadable.Add(repo.Value);
        return this;
    }

    /// <inheritdoc/>
    public ValueTask<Result<bool>> Reaches(ContextRepo repo, string commit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);
        return ValueTask.FromResult<Result<bool>>(_unreadable.Contains(repo.Name.Value)
            ? ContextErrors.ExtractionUnreadable.With("the fake's checkout is unreadable")
            : _onDefault.TryGetValue(repo.Name.Value, out var commits) && commits.Contains(commit));
    }
}
