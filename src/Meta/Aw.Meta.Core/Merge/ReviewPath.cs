using System.Globalization;

namespace Aw.Meta.Core.Merge;

/// <summary>The review and counts checks of the merge gate's review path.</summary>
internal static class ReviewPath
{
    private const string NoCountLine = "the ready message has no \"N passed, M skipped, K failed\" line";

    /// <summary>The counts check: no failed count, and at least one count line (review.mode counts: tests passed, or the build-only marker).</summary>
    /// <param name="review">The review facts.</param>
    /// <returns>The "counts" check.</returns>
    public static MergeCheck Counts(ReviewFacts review)
    {
        var failed = review.Counts.Where(c => c.Failed > 0).ToList();
        return failed.Count > 0
            ? new MergeCheck("counts", Ok: false, $"the ready message counts {failed.Sum(c => c.Failed).ToString(CultureInfo.InvariantCulture)} failed")
            : review.CountsMode
                ? CountsModeCounts(review)
                : review.Counts.Count > 0
                    ? new MergeCheck("counts", Ok: true, $"{review.Counts.Count.ToString(CultureInfo.InvariantCulture)} count line(s), 0 failed")
                    : new MergeCheck("counts", Ok: false, NoCountLine);
    }

    // review.mode counts, with no failed count: the ready message is the only proof, so its count lines must pass at least one
    // test. A filtered run that matched nothing counts zero and refuses. A line that is exactly
    // "0 passed, 0 skipped, 0 failed (build only)" stands in for the tests when the lane built and ran none (#3840).
    private static MergeCheck CountsModeCounts(ReviewFacts review)
    {
        if (review.Counts.Sum(c => c.Passed) > 0)
        {
            return new MergeCheck("counts", Ok: true, $"{review.Counts.Count.ToString(CultureInfo.InvariantCulture)} count line(s), 0 failed");
        }

        if (review.BuildOnly)
        {
            return new MergeCheck("counts", Ok: true, "build only, no test counts");
        }

        return review.Counts.Count > 0
            ? new MergeCheck("counts", Ok: false, $"the ready message's count lines total 0 passed; a lane that ran no test puts \"{ReviewProof.BuildOnlyLine}\" on a line of its own")
            : new MergeCheck("counts", Ok: false, NoCountLine);
    }

    /// <summary>The review check. The reviewer's standing OK must name the head; a qa line under it ("qa not yet", "qa passed
    /// sha") must say passed at the head. In review.mode counts no OK is needed: a standing HOLD refuses, and so does the qa line
    /// under the standing OK wherever that OK points, so a push does not lift a qa block (#3840). A later OK with no qa line, or
    /// with "qa passed" at the head, clears it.</summary>
    /// <param name="review">The review facts.</param>
    /// <param name="pr">The pull request number, as text.</param>
    /// <param name="head">The pull request's head.</param>
    /// <returns>The "review" check.</returns>
    public static MergeCheck Review(ReviewFacts review, string pr, string head)
    {
        if (review.Hold is not null)
        {
            return new MergeCheck("review", Ok: false, $"{review.Reviewer} holds #{pr}: \"{review.Hold}\"; a later \"OK #{pr} at <sha>\" lifts it");
        }

        var okAtHead = ReviewProof.IsHead(review.OkSha, head);
        var refused = review.CountsMode ? null : OkModeRefusal(review, pr, head, okAtHead);
        if (refused is not null)
        {
            return refused;
        }

        return QaRefusal(review, head) ?? (okAtHead
            ? new MergeCheck("review", Ok: true, $"{review.Reviewer} said OK at {review.OkSha}" + (review.QaSha is null ? string.Empty : $", qa passed at {review.QaSha}"))
            : new MergeCheck("review", Ok: true, $"review.mode counts: no OK needed, no HOLD from {review.Reviewer}"));
    }

    // review.mode ok: the standing OK must exist and name the head. Null when it does.
    private static MergeCheck? OkModeRefusal(ReviewFacts review, string pr, string head, bool okAtHead)
    {
        if (review.OkSha is null)
        {
            return new MergeCheck("review", Ok: false, $"no standing \"OK #{pr} at <sha>\" from {review.Reviewer}");
        }

        return okAtHead ? null : new MergeCheck("review", Ok: false, $"{review.Reviewer}'s newest OK is at {review.OkSha}, the head is {head}");
    }

    // The qa line under the standing OK, in both modes: "qa not yet" refuses, "qa passed <sha>" must name the head. Null when qa allows it.
    private static MergeCheck? QaRefusal(ReviewFacts review, string head)
    {
        if (review.QaNotYet)
        {
            return new MergeCheck("review", Ok: false, $"{review.Reviewer} said OK at {review.OkSha} but qa not yet");
        }

        return review.QaSha is not null && !ReviewProof.IsHead(review.QaSha, head)
            ? new MergeCheck("review", Ok: false, $"qa passed at {review.QaSha}, the head is {head}")
            : null;
    }
}
