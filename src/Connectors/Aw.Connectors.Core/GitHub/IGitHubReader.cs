using Aw.Kernel;

namespace Aw.Connectors.Core.GitHub;

/// <summary>Reads a repo's issues, pull requests and comments changed since a cursor.</summary>
public interface IGitHubReader
{
    /// <summary>Reads the entries of <paramref name="repo"/> changed at or after the cursor, skipping the ones the cursor has seen. Whole pages are read, so slightly more than <paramref name="limit"/> entries can come back.</summary>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="after">Where to start; <see cref="GitHubCursor.From"/> for a first run.</param>
    /// <param name="limit">Stop after the page that brings the run to this many entries.</param>
    /// <param name="cancellationToken">The caller's cancellation.</param>
    /// <returns>The page (a rate limit is a value in it, not an error), or an error from the Connectors catalogue.</returns>
    public ValueTask<Result<GitHubPullPage>> Issues(string repo, GitHubCursor after, int limit, CancellationToken cancellationToken);

    /// <summary>Reads the comments of every issue and pull request of <paramref name="repo"/> changed in [<paramref name="since"/>, <paramref name="until"/>], oldest change first, in whole pages.</summary>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="since">The start (inclusive).</param>
    /// <param name="until">The end (inclusive); the read stops after the page that passes it.</param>
    /// <param name="cancellationToken">The caller's cancellation.</param>
    /// <returns>The comments (a rate limit is a value in the page), or an error from the Connectors catalogue.</returns>
    public ValueTask<Result<GitHubCommentPage>> Comments(string repo, DateTimeOffset since, DateTimeOffset until, CancellationToken cancellationToken);

    /// <summary>Reads the comments of one issue or pull request, oldest first, at most ten pages of 100.</summary>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="number">The issue or pull request number.</param>
    /// <param name="cancellationToken">The caller's cancellation.</param>
    /// <returns>The comments (a rate limit is a value in the page), or an error from the Connectors catalogue.</returns>
    public ValueTask<Result<GitHubCommentPage>> IssueComments(string repo, int number, CancellationToken cancellationToken);

    /// <summary>Reads a pull request's merge state and changed paths (GET /pulls/{n} and /pulls/{n}/files).</summary>
    /// <param name="repo">'owner/name'.</param>
    /// <param name="number">The pull request number.</param>
    /// <param name="cancellationToken">The caller's cancellation.</param>
    /// <returns>The detail (a rate limit is a value in the result), or an error from the Connectors catalogue.</returns>
    public ValueTask<Result<GitHubPullDetailRead>> PullRequest(string repo, int number, CancellationToken cancellationToken);
}
