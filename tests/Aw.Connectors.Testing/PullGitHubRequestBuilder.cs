using Aw.Connectors.Core.GitHub;
using Aw.Connectors.Features.PullGitHub;

namespace Aw.Connectors.Testing;

/// <summary>Builds a PullGitHubRequest with deterministic defaults.</summary>
public sealed class PullGitHubRequestBuilder
{
    private string _repo = "acme/widgets";
    private GitHubCursor _after = GitHubCursor.From(new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero));
    private int _limit = 100;
    private int _commentCap = GitHubSettings.DefaultCommentCap;

    /// <summary>Sets the repo.</summary>
    /// <param name="repo">'owner/name'.</param>
    /// <returns>This builder.</returns>
    public PullGitHubRequestBuilder ForRepo(string repo)
    {
        _repo = repo;
        return this;
    }

    /// <summary>Sets where the read starts.</summary>
    /// <param name="after">The cursor.</param>
    /// <returns>This builder.</returns>
    public PullGitHubRequestBuilder After(GitHubCursor after)
    {
        _after = after;
        return this;
    }

    /// <summary>Sets the limit.</summary>
    /// <param name="limit">Entries to stop at.</param>
    /// <returns>This builder.</returns>
    public PullGitHubRequestBuilder WithLimit(int limit)
    {
        _limit = limit;
        return this;
    }

    /// <summary>Sets the most comments read for one record.</summary>
    /// <param name="cap">The cap, 0 to read none.</param>
    /// <returns>This builder.</returns>
    public PullGitHubRequestBuilder WithCommentCap(int cap)
    {
        _commentCap = cap;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public PullGitHubRequest Build() => new(_repo, _after, _limit) { CommentCap = _commentCap };
}
