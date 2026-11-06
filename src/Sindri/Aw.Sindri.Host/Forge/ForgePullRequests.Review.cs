using Aw.Kernel;
using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Host.Forge;

internal sealed partial class ForgePullRequests
{
    public ValueTask<Result<PullFiles>> Files(string link, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<string>> FileText(string link, string path, string sha, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<PullReviewOutcome>> SubmitReview(PullReview review, CancellationToken cancellationToken) => throw new NotSupportedException();
}
