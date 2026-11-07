using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.SendReviewFix;

/// <summary>
/// Sends a finding as a fix task (PR review page spec §8.2). In order: the PR is checked (<see cref="FixTargets"/>); the item is made in Work (a Retry reuses it); the finding is posted as a thread when the PR is
/// the owner's or an agent's (a failure is the half-done answer, never an error); the lane is found; the fix-task file of the head gets the item; 'review.fix-sent' is recorded.
/// The owner's own action: a launched session is refused (SND-AUTH-002).
/// </summary>
/// <param name="callers">The caller.</param>
/// <param name="targets">Checks the PR.</param>
/// <param name="items">Makes the item.</param>
/// <param name="poster">Posts the thread.</param>
/// <param name="route">Finds the lane.</param>
/// <param name="files">The fix-task files.</param>
/// <param name="recorder">Records the event.</param>
internal sealed class SendReviewFixService(ICallerContextAccessor callers, FixTargets targets, IFixItems items, FixThreadPoster poster, FixRoute route, IFixTaskFiles files, FixSentRecorder recorder)
    : IService<SendReviewFixRequest, SendReviewFixResponse>
{
    public async ValueTask<Result<SendReviewFixResponse>> Handle(SendReviewFixRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (callers.Current is not { } caller || caller.Class == CallerClass.Session)
        {
            return SindriErrors.AuthRefused.With("sending a fix task is the owner's; a session can't (fix: use the Send button on the review page)");
        }

        var resolved = await targets.Resolve(request, cancellationToken).ConfigureAwait(false);
        if (resolved.IsError)
        {
            return resolved.Error;
        }

        var target = resolved.Value;
        var made = await ItemFor(request, target, cancellationToken).ConfigureAwait(false);
        if (made.IsError)
        {
            return made.Error;
        }

        var (item, created) = made.Value;
        var posted = target.PostsThread ? await poster.Post(FixItemText.Review(request, target.Link, item.Value), item.Value, request.Number, cancellationToken).ConfigureAwait(false) : FixPosted.Skipped;
        var (went, session) = await route.Find(target.Link, target.HeadRepo, target.Branch, cancellationToken).ConfigureAwait(false);
        var filed = created ? await files.Add(FixHead.Of(target.HeadRepo, target.Branch), item.Value, request.Number, cancellationToken).ConfigureAwait(false) : Unit.Value;
        if (filed.IsError)
        {
            return filed.Error;
        }

        var recorded = await recorder.Record(caller, new ReviewFixSent(caller.CommandId, item, request.Number, posted.Thread, went, posted.Code), cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : new SendReviewFixResponse(item, posted.Posted, posted.Code, posted.Error, went, session);
    }

    private async ValueTask<Result<(ItemId Item, bool Created)>> ItemFor(SendReviewFixRequest request, FixTarget target, CancellationToken cancellationToken)
    {
        if (request.Item is { } existing)
        {
            return ItemId.TryFrom(existing, out var reused)
                ? (reused, false)
                : SindriErrors.InvalidInput.With("the item is not an item id (fix: the id the first send answered with)");
        }

        var head = FixHead.Of(target.HeadRepo, target.Branch);
        var made = await items.Create(FixItemText.Title(request.Text), target.Repo, FixItemText.Body(request, target.Slug, target.Link, head), cancellationToken).ConfigureAwait(false);
        return made.IsError ? made.Error : (made.Value, true);
    }
}
