using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Reviews;

namespace Aw.Sindri.Features.SendReviewFix;

/// <summary>
/// Sends a finding as a fix task (PR review page spec §8.2). In order: the PR is checked (<see cref="FixTargets"/>); the item is made in Work (a retry reuses it); the finding is posted as a thread when the PR is
/// the owner's or an agent's (a failure is the half-done answer, never an error); the lane is found; the fix-task file of the head gets the item (a retry writes it again, once; a write that fails is a note on the answer, never an error); 'review.fix-sent' is recorded.
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

        var item = made.Value;
        var posted = target.PostsThread ? await poster.Post(FixItemText.Review(request, target.Link, item.Value), item.Value, request.Number, cancellationToken).ConfigureAwait(false) : FixPosted.Skipped;
        var (went, session) = await route.Find(target.Link, target.HeadRepo, target.Branch, cancellationToken).ConfigureAwait(false);
        string? fileError = null;
        if (Head(target) is { } head && (await files.Add(head, item, request.Number, cancellationToken).ConfigureAwait(false)) is { IsError: true } filed)
        {
            // why: the item and the thread exist by now; failing here would hide the item id the retry needs, so the answer carries the miss and a retry (which reuses the item) writes the file
            fileError = $"The fix-task file wasn't written ({filed.Error.Message}); send it again to write it.";
        }

        var recorded = await recorder.Record(caller, new ReviewFixSent(caller.CommandId, item, request.Number, posted.Thread, went, posted.Code), cancellationToken).ConfigureAwait(false);
        return recorded.IsError ? recorded.Error : new SendReviewFixResponse(item, posted.Posted, posted.Code, posted.Error, went, session, fileError);
    }

    private static string? Head(FixTarget target) => target.HeadRepo is { } repo && target.Branch is { } branch ? FixHead.Of(repo, branch) : null;

    private async ValueTask<Result<ItemId>> ItemFor(SendReviewFixRequest request, FixTarget target, CancellationToken cancellationToken)
    {
        if (request.Item is { } existing)
        {
            return ItemId.TryFrom(existing, out var reused)
                ? reused
                : SindriErrors.InvalidInput.With("the item is not an item id (fix: the id the first send answered with)");
        }

        var made = await items.Create(FixItemText.Title(request.Text), target.Repo, FixItemText.Body(request, target.Slug, target.Link, Head(target)), cancellationToken).ConfigureAwait(false);
        return made.IsError ? made.Error : made.Value;
    }
}
