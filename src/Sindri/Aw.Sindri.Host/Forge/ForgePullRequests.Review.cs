using System.Diagnostics;
using Aw.Forge.Core.Pulls;
using Aw.Kernel;
using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Host.Forge;

internal sealed partial class ForgePullRequests
{
    public async ValueTask<Result<Core.Forge.PullFiles>> Files(string link, CancellationToken cancellationToken)
    {
        var pull = forge.Parse(link);
        if (pull.IsError)
        {
            return PullErrors.From(pull.Error);
        }

        var files = await forge.Files(pull.Value, cancellationToken).ConfigureAwait(false);
        return files.IsError
            ? PullErrors.From(files.Error)
            : new Core.Forge.PullFiles([.. files.Value.Files.Select(static f => new Core.Forge.PullFile(f.Path, f.PreviousPath, FileStatus(f.Status), f.Additions, f.Deletions, f.Patch))], files.Value.CapReached);
    }

    public async ValueTask<Result<string>> FileText(string link, string path, string sha, CancellationToken cancellationToken)
    {
        var pull = forge.Parse(link);
        if (pull.IsError)
        {
            return PullErrors.From(pull.Error);
        }

        var text = await forge.FileText(pull.Value, path, sha, cancellationToken).ConfigureAwait(false);
        return text.IsError ? PullErrors.From(text.Error) : text.Value;
    }

    public async ValueTask<Result<PullReviewOutcome>> SubmitReview(PullReview review, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(review);
        var pull = forge.Parse(review.Link);
        if (pull.IsError)
        {
            return PullErrors.From(pull.Error);
        }

        var request = new SubmitReviewRequest(pull.Value, review.ExpectedHead, Verdict(review.Verdict), review.Body, [.. review.Comments.Select(static c => new ReviewComment(c.Path, c.Line, c.Body))], review.Marker);
        var outcome = await forge.SubmitReview(request, cancellationToken).ConfigureAwait(false);
        return outcome.IsError ? PullErrors.From(outcome.Error) : ReviewOutcomeOf(outcome.Value);
    }

    private static Core.Forge.PullFileStatus FileStatus(Aw.Forge.Core.Pulls.PullFileStatus status) => status switch
    {
        Aw.Forge.Core.Pulls.PullFileStatus.Added => Core.Forge.PullFileStatus.Added,
        Aw.Forge.Core.Pulls.PullFileStatus.Modified => Core.Forge.PullFileStatus.Modified,
        Aw.Forge.Core.Pulls.PullFileStatus.Removed => Core.Forge.PullFileStatus.Removed,
        Aw.Forge.Core.Pulls.PullFileStatus.Renamed => Core.Forge.PullFileStatus.Renamed,
        Aw.Forge.Core.Pulls.PullFileStatus.Copied => Core.Forge.PullFileStatus.Copied,
        Aw.Forge.Core.Pulls.PullFileStatus.Changed => Core.Forge.PullFileStatus.Changed,
        Aw.Forge.Core.Pulls.PullFileStatus.Unchanged => Core.Forge.PullFileStatus.Unchanged,
        _ => throw new UnreachableException($"file status {status} has no mapping"),
    };

    private static ReviewVerdict Verdict(PullReviewVerdict verdict) => verdict switch
    {
        PullReviewVerdict.Comment => ReviewVerdict.Comment,
        PullReviewVerdict.Approve => ReviewVerdict.Approve,
        PullReviewVerdict.RequestChanges => ReviewVerdict.RequestChanges,
        _ => throw new UnreachableException($"review verdict {verdict} has no mapping"),
    };

    private static PullReviewOutcome ReviewOutcomeOf(ReviewOutcome outcome) => outcome switch
    {
        ReviewOutcome.Posted => PullReviewOutcome.Posted,
        ReviewOutcome.AlreadyDelivered => PullReviewOutcome.AlreadyDelivered,
        ReviewOutcome.HeadMoved => PullReviewOutcome.HeadMoved,
        ReviewOutcome.LineRejected => PullReviewOutcome.LineRejected,
        _ => throw new UnreachableException($"review outcome {outcome} has no mapping"),
    };
}
