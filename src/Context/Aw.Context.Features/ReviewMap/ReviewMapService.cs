using Aw.Context.Core;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ReviewMap;

/// <summary>
/// One review decision (spec §8.7): checked against the latest draft by <see cref="ReviewPlanner"/> (SND-CONTEXT-047 when the
/// target is gone or decided already), then appended as override rows, one per decision (accept-deterministic: one per entry),
/// all or none. A suggested file link may also be decided, and any decision undone, with no draft: the current map is what its filemap shows.
/// Sindri checks the verb's class first (X63); a session is refused here (SND-CONTEXT-020) only for the bulk accept.
/// </summary>
internal sealed class ReviewMapService(IGraphStore store, ICurrentUser user, TimeProvider clock) : IService<ReviewMapRequest, ReviewResult>
{
    public async ValueTask<Result<ReviewResult>> Handle(ReviewMapRequest request, CancellationToken cancellationToken)
    {
        if (MapAccess.Gate(request.Scope) is { } refused)
        {
            return refused;
        }

        var basis = await Basis(request.Decision, cancellationToken).ConfigureAwait(false);
        if (basis.IsError)
        {
            return basis.Error;
        }

        // The decision is planned against what the caller may see: a bulk accept writes only the items shown, and the answer's count is the queue's (review #220 run 3).
        var (map, onDraft) = basis.Value;
        var seen = MapAccess.View(map.Graph, map.Overrides, request.Scope, pending: true, callerScope: false);
        if (seen.IsError)
        {
            return seen.Error;
        }

        var visible = new ReviewBasis(seen.Value.Graph, seen.Value.Overrides, onDraft);
        var rows = ReviewPlanner.Plan(request.Decision, visible, ReviewDrafts.Who(request.Scope.Caller, user), clock.GetUtcNow());
        if (rows.IsError)
        {
            return rows.Error;
        }

        var added = await store.AppendOverrides(rows.Value, cancellationToken).ConfigureAwait(false);
        if (added.IsError)
        {
            return added.Error;
        }

        var undecided = ReviewBacklog.Build(map.Label, visible.Graph, [.. visible.Overrides, .. rows.Value]).Undecided;
        return new ReviewResult([.. rows.Value.Select(r => r.ToId())], [], undecided);
    }

    private async ValueTask<Result<(MapSnapshot Map, bool OnDraft)>> Basis(ReviewDecision decision, CancellationToken cancellationToken)
    {
        var draft = await ReviewDrafts.Draft(store, cancellationToken).ConfigureAwait(false);
        if (draft.IsSuccess)
        {
            return (draft.Value, true);
        }

        if (!(decision.Link is not null || decision.Verb is ReviewVerb.Undo or ReviewVerb.Drop or ReviewVerb.Repoint) || draft.Error.Code != ContextErrors.NoDraftMap.Code)
        {
            return draft.Error;
        }

        var current = await store.Snapshot(draft: false, cancellationToken).ConfigureAwait(false);
        if (current.IsError)
        {
            return current.Error;
        }

        return current.Value is { } found ? (found, false) : ContextErrors.NoMap.With("there is no map yet (fix: sindri context map import <file>)");
    }
}
