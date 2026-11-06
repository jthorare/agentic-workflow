namespace Aw.Sindri.Core.Forge;

/// <summary>A review to post on a pull request, only if its head is still the one the reviewer read.</summary>
/// <param name="Link">The pull request's link.</param>
/// <param name="ExpectedHead">The head sha the review was written against.</param>
/// <param name="Verdict">Comment, approve or request changes.</param>
/// <param name="Body">The review's text.</param>
/// <param name="Comments">The line comments.</param>
/// <param name="Marker">Makes the review post once: a second call with the same marker is <see cref="PullReviewOutcome.AlreadyDelivered"/>.</param>
public sealed record PullReview(string Link, string ExpectedHead, PullReviewVerdict Verdict, string Body, IReadOnlyList<PullReviewComment> Comments, string Marker);
