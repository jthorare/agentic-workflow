namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>GET /sindri/reviews (review page spec §6.1): the owner's open PRs and the ones asking for the owner's review, in two groups. The answer's asOf is not read: the status carries the freshness the app shows.</summary>
/// <param name="Login">The review.login the list is for; null until it is set.</param>
/// <param name="LoginPrompt">The one-field login prompt (SND-REVIEW-007) while review.login is unset; null otherwise.</param>
/// <param name="Notice">A line above the list, such as the before-reviewers one (§5.6); null for none.</param>
/// <param name="Freshness">One entry per source.</param>
/// <param name="Stale">"PR list is from …" when a source failed and the rows are the last ones; null otherwise.</param>
/// <param name="Library">The library header ("Library: 41 docs, 3 with errors").</param>
/// <param name="WaitingCount">The Waiting on you group's size: Home's line and the tab's badge.</param>
/// <param name="Groups">Waiting on you, then Yours; a group with no rows is left out.</param>
/// <param name="Empty">The empty state's lines when there are no rows; null otherwise.</param>
internal sealed record ReviewList(
    string? Login,
    ReviewLoginPromptView? LoginPrompt,
    string? Notice,
    IReadOnlyList<ReviewFreshnessView> Freshness,
    ReviewStaleView? Stale,
    ReviewLibraryView Library,
    int WaitingCount,
    IReadOnlyList<ReviewGroupView> Groups,
    ReviewEmptyView? Empty);
