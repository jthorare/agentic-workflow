using Aw.Connectors.Core.GitHub;
using Aw.Kernel;

namespace Aw.Connectors.Infrastructure.GitHub;

/// <summary>The review read of <see cref="GitHubClient"/>: GET /pulls/{n}/reviews, then one GET /compare per reviewed commit that is not the head.</summary>
internal sealed partial class GitHubClient
{
    /// <inheritdoc />
    public ValueTask<Result<GitHubReviewRead>> PullReviews(string repo, int number, string head, CancellationToken cancellationToken)
        => throw new NotSupportedException();
}
