using Aw.Context.Core.Graph;
using Aw.Context.Core.Map;
using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ReviewHistory;

/// <summary>The override log of the map, newest first, with what each undo cancelled (spec §8.7 'map review --history'). A session is refused like the rest of the review (SND-CONTEXT-020).</summary>
internal sealed class ReviewHistoryService(IGraphStore store) : IService<ReviewHistoryRequest, ReviewHistoryView>
{
    public async ValueTask<Result<ReviewHistoryView>> Handle(ReviewHistoryRequest request, CancellationToken cancellationToken)
    {
        if (MapAccess.Gate(request.Scope) is { } refused)
        {
            return refused;
        }

        var limit = ReviewLimits.Resolve(request.Limit, Core.Review.ReviewHistory.DefaultLimit);
        var overrides = limit.IsError ? limit.Error : await store.Overrides(cancellationToken).ConfigureAwait(false);
        var graph = overrides.IsError ? overrides.Error : await Graph(cancellationToken).ConfigureAwait(false);
        var seen = graph.IsError ? graph.Error : MapAccess.View(graph.Value, overrides.Value, request.Scope, pending: true, callerScope: false);
        return limit.IsError || overrides.IsError || seen.IsError
            ? (limit.IsError ? limit.Error : overrides.IsError ? overrides.Error : seen.Error)
            : Core.Review.ReviewHistory.Of(seen.Value.Overrides, limit.Value);
    }

    // The graph the deny globs are judged on: the latest draft, else the current map, else nothing (an empty history is not a leak).
    private async ValueTask<Result<GraphSnapshot>> Graph(CancellationToken cancellationToken)
    {
        var draft = await store.Snapshot(draft: true, cancellationToken).ConfigureAwait(false);
        var found = draft.IsError || draft.Value is not null ? draft : await store.Snapshot(draft: false, cancellationToken).ConfigureAwait(false);
        return found.IsError ? found.Error : found.Value?.Graph ?? GraphSnapshot.Empty;
    }
}
