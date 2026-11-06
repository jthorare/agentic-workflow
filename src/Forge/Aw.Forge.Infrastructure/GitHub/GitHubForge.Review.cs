using Aw.Forge.Core.Pulls;
using Aw.Forge.Core.Refs;
using Aw.Kernel;

namespace Aw.Forge.Infrastructure.GitHub;

internal sealed partial class GitHubForge
{
    public ValueTask<Result<PullFiles>> Files(ForgeRef pull, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<string>> FileText(ForgeRef pull, string path, string sha, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<ReviewOutcome>> SubmitReview(SubmitReviewRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
