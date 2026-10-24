using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Context.Core.Review;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ApplyReviewFile;

/// <summary>
/// Applies an edited review file to the latest draft (spec §8.7 'map review --apply'): each decided entry that still sits in the
/// queue becomes override rows, the rest are listed as skipped; SND-CONTEXT-043 only when nothing matches. An entry the
/// draft no longer allows (it changed since the file was read) is skipped too. Sindri checks the caller first (protected).
/// </summary>
internal sealed class ApplyReviewFileService(IGraphStore store, IReviewFileStore files, ICurrentUser user, TimeProvider clock) : IService<ApplyReviewFileRequest, ReviewResult>
{
    public async ValueTask<Result<ReviewResult>> Handle(ApplyReviewFileRequest request, CancellationToken cancellationToken)
    {
        if (MapAccess.Gate(request.Scope) is { } refused)
        {
            return refused;
        }

        var file = await files.Read(request.Path, cancellationToken).ConfigureAwait(false);
        var draft = file.IsError ? file.Error : await ReviewDrafts.Draft(store, cancellationToken).ConfigureAwait(false);
        if (file.IsError || draft.IsError)
        {
            return file.IsError ? file.Error : draft.Error;
        }

        var prepared = Prepare(draft.Value, file.Value, request.Scope);
        if (prepared.IsError)
        {
            return prepared.Error;
        }

        cancellationToken.ThrowIfCancellationRequested();   // a Ctrl-C ends the request: stop before planning, not after it
        var (basis, matched) = prepared.Value;
        var (planned, skippedLater) = ReviewPlanner.PlanAll(matched.Decisions, basis, ReviewDrafts.Who(request.Scope.Caller, user), clock.GetUtcNow(), cancellationToken);
        var rows = planned.ToList();
        var skipped = matched.Skipped.Concat(skippedLater).ToList();
        cancellationToken.ThrowIfCancellationRequested();
        var added = await store.AppendOverrides(rows, cancellationToken).ConfigureAwait(false);
        return added.IsError
            ? added.Error
            : new ReviewResult([.. rows.Select(r => r.ToId())], skipped, ReviewBacklog.Build(draft.Value.Label, basis.Graph, [.. basis.Overrides, .. rows]).Undecided);
    }

    // What the file may decide: the entries that match the queue the caller sees, so a hidden item can't be matched, decided or counted.
    private static Result<(ReviewBasis Basis, ReviewFileMatch Matched)> Prepare(MapSnapshot draft, ReviewFile file, ContextScope scope)
    {
        var seen = ReviewDrafts.Visible(draft, scope);
        var matched = seen.IsError ? seen.Error : ReviewFileMatcher.Match(file, seen.Value.Queue);
        return seen.IsError || matched.IsError
            ? (seen.IsError ? seen.Error : matched.Error)
            : (new ReviewBasis(seen.Value.View.Graph, seen.Value.View.Overrides), matched.Value);
    }
}
