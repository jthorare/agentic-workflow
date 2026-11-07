namespace Aw.Sindri.Ui.Api.Reviews;

/// <summary>GET /sindri/reviews/{owner}/{repo}/{number} (review page spec §6.2): the page model. The answer's asOf is not read.</summary>
/// <param name="Pr">The PR's header facts.</param>
/// <param name="Settings">The review settings the page was built from; null when none apply.</param>
/// <param name="Moving">Set when the head moved twice during the read: "The PR is moving; reload when it settles."</param>
/// <param name="Offline">Set when the page is the cached one: "as of 23:45, the PR may have moved".</param>
/// <param name="Strip">The summary strip's parts, in order, each naming its panel.</param>
/// <param name="Panels">The panels in settings order.</param>
/// <param name="Diff">The diff, the right pane.</param>
internal sealed record ReviewPage(
    ReviewPrView Pr,
    ReviewSettingsView? Settings,
    ReviewMovingView? Moving,
    ReviewOfflineView? Offline,
    IReadOnlyList<ReviewStripPartView> Strip,
    IReadOnlyList<ReviewPanelView> Panels,
    ReviewDiffView Diff);
