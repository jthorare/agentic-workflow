using Aw.Context.Core.Map;
using Aw.Context.Core.Review;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ReviewQueue;

/// <summary>What is left to decide in the latest draft: deterministic proposals, model ones and suggested links by impact, then orphaned overrides (spec §8.7), each group cut at the call's limit (the counts stay full).</summary>
internal sealed class ReviewQueueService(IGraphStore store, IScrubberFactory scrubbers) : IService<ReviewQueueRequest, ReviewQueueView>
{
    public async ValueTask<Result<ReviewQueueView>> Handle(ReviewQueueRequest request, CancellationToken cancellationToken)
    {
        var limit = ReviewLimits.Resolve(request.Limit, ReviewLimits.QueueDefault);
        if (limit.IsError)
        {
            return limit.Error;
        }

        var loaded = await ReviewDrafts.Load(store, scrubbers, request.Scope, cancellationToken).ConfigureAwait(false);
        return loaded.Map(l => l.Queue.Capped(limit.Value));
    }
}
