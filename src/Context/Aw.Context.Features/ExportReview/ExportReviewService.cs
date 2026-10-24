using System.Globalization;
using Aw.Context.Core.Map;
using Aw.Context.Core.Review;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ExportReview;

/// <summary>Writes the latest draft's queue as a review file under the exports folder (spec §8.7 'map review --export'): the owner edits it and applies it with the apply-file command.</summary>
internal sealed class ExportReviewService(IGraphStore store, IScrubberFactory scrubbers, IReviewFileStore files, TimeProvider clock) : IService<ExportReviewRequest, ReviewExported>
{
    public async ValueTask<Result<ReviewExported>> Handle(ExportReviewRequest request, CancellationToken cancellationToken)
    {
        var loaded = await ReviewDrafts.Load(store, scrubbers, request.Scope, cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error;
        }

        var (draft, queue) = loaded.Value;
        var file = ReviewFile.Of(queue);
        var name = string.Create(CultureInfo.InvariantCulture, $"review-v{draft.Version.Number}-{clock.GetUtcNow().UtcDateTime:yyyyMMdd'T'HHmmss'Z'}.json");
        var written = await files.Write(request.Path, name, file, cancellationToken).ConfigureAwait(false);
        return written.IsError ? written.Error : new ReviewExported(written.Value, file.Entries.Count);
    }
}
