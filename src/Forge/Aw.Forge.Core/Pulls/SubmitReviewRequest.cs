using Aw.Forge.Core.Refs;

namespace Aw.Forge.Core.Pulls;

/// <summary>A review to post on a pull request, only if its head is still the one the reviewer read.</summary>
/// <param name="Pull">The pull request.</param>
/// <param name="ExpectedHead">The head sha the review was written against.</param>
/// <param name="Verdict">Comment, approve or request changes.</param>
/// <param name="Body">The review's text; the caller has already redacted it.</param>
/// <param name="Comments">The line comments.</param>
/// <param name="Marker">The idempotency marker (ForgeMarker); the review is posted once per marker.</param>
public sealed record SubmitReviewRequest(ForgeRef Pull, string ExpectedHead, ReviewVerdict Verdict, string Body, IReadOnlyList<ReviewComment> Comments, string Marker);
