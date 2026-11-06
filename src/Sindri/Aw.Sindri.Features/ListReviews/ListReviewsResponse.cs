using Aw.Sindri.Core.Projects.Cards;
using Aw.Sindri.Core.Reviews;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ListReviews;

/// <summary>The Reviews list (PR review page spec §6.1): the owner's open PRs and the ones asking for the owner's review, in two groups.</summary>
/// <param name="AsOf">How fresh the views are.</param>
/// <param name="Login">The <c language="text">review.login</c> the list is for; null until it is set.</param>
/// <param name="LoginPrompt">The login prompt while <paramref name="Login"/> is null; null otherwise.</param>
/// <param name="Notice">A line above the list (before reviewers are read, §5.6); null for none.</param>
/// <param name="Freshness">One entry per source.</param>
/// <param name="Stale">"PR list is from …" when GitHub failed and the rows are the last ones; null otherwise.</param>
/// <param name="Library">The library header.</param>
/// <param name="WaitingCount">The Waiting on you group's size.</param>
/// <param name="Groups">Waiting on you, then Yours; none before the login is set.</param>
/// <param name="Empty">The empty state when there are no rows; null otherwise.</param>
public sealed record ListReviewsResponse(
    AsOf AsOf,
    string? Login,
    ReviewLoginPrompt? LoginPrompt,
    string? Notice,
    IReadOnlyList<ProjectFreshnessRow> Freshness,
    ReviewStale? Stale,
    ReviewLibrary Library,
    int WaitingCount,
    IReadOnlyList<ReviewListGroup> Groups,
    ReviewEmpty? Empty);
