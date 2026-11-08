namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>The fix sheet's outcomes (<see cref="ReviewFixSheetViewModel"/>, continued; review page spec §8.2, §16).</summary>
internal sealed partial class ReviewFixSheetViewModel
{
    private void Show(Api.Reviews.ReviewFixSent sent, ReviewFixRequestedEventArgs request)
    {
        var (source, target) = (request.Source, request.Target);
        (ItemId, _item) = (sent.ItemId, sent.ItemId);
        CanSend = false;
        if (sent.PostError is { } error)
        {
            (Outcome, Heading, OutcomeText, Code, ThreadResult, CanRetry) = (ReviewFixOutcome.HalfDone, Labels.ReviewFixHalfHeading, error, sent.PostCode ?? string.Empty, Labels.ReviewFixNotPosted, true);
        }
        else if (!sent.Posted)
        {
            (Outcome, Heading, OutcomeText, ThreadResult, CanRetry) = (ReviewFixOutcome.ItemOnly, Labels.ReviewFixSentHeading, Labels.ReviewFixItemOnlyOutcome(target.Author, target.Branch), Labels.ReviewFixNotPostedContributor, false);
        }
        else
        {
            var where = source.Kind == ReviewFixKind.Line && !source.Removed ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{Path.GetFileName(source.Path)}:{source.Line}") : Labels.ReviewFixReviewComment;
            var (outcome, text) = sent.Went switch
            {
                "session" => (ReviewFixOutcome.Session, Labels.ReviewFixSession(target.Branch, sent.Session ?? string.Empty)),
                _ => (ReviewFixOutcome.Waiting, Labels.ReviewFixWaiting(target.Branch)),
            };
            (Outcome, Heading, OutcomeText, ThreadResult, Code, CanRetry) = (outcome, Labels.ReviewFixSentHeading, text, Labels.ReviewFixThread(target.Number, where), string.Empty, false);
        }

        OnChanged(nameof(HasOutcome));
        Focus = new ReviewFocus(ReviewFocusRegion.FixOutcome);
    }
}
