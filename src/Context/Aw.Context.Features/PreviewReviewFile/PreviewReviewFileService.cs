using Aw.Context.Core.Map;
using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.PreviewReviewFile;

/// <summary>Reads a review file and answers its hash and counts, writing nothing (spec §8.7 'map review --apply' shows this before the typed confirmation). A session is refused (SND-CONTEXT-020).</summary>
internal sealed class PreviewReviewFileService(IReviewFileStore files) : IService<PreviewReviewFileRequest, ReviewFilePreview>
{
    public async ValueTask<Result<ReviewFilePreview>> Handle(PreviewReviewFileRequest request, CancellationToken cancellationToken)
    {
        if (MapAccess.Gate(request.Scope) is { } refused)
        {
            return refused;
        }

        var file = await files.Read(request.Path, cancellationToken).ConfigureAwait(false);
        return file.IsError ? file.Error : new ReviewFilePreview(file.Value.Fingerprint(), file.Value.Entries.Count, file.Value.Entries.Count(e => e.Decision is not null));
    }
}
