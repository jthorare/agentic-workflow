using Aw.Forge.Core.Pulls;
using Aw.Forge.Core.Refs;
using Aw.Kernel;

namespace Aw.Forge.Infrastructure.World;

/// <summary>The review page's reads and the review post: the files, their texts and SubmitReview.</summary>
internal partial class WorldForge
{
    public ValueTask<Result<PullFiles>> Files(ForgeRef pull, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<string>> FileText(ForgeRef pull, string path, string sha, CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask<Result<ReviewOutcome>> SubmitReview(SubmitReviewRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Result<Unit> SeedFiles(SeedFilesRequest request) => throw new NotSupportedException();
}
