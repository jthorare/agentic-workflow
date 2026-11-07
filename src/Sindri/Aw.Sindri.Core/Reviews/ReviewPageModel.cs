using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Reviews;

/// <summary>GET /sindri/reviews/{owner}/{repo}/{number} (PR review page spec §6.2): the page model.</summary>
/// <param name="AsOf">How fresh the views are.</param>
/// <param name="Pr">The PR header.</param>
/// <param name="Settings">The review settings the page was built from.</param>
/// <param name="Moving">Set when the head moved twice during the read; null otherwise.</param>
/// <param name="Offline">Set when the page is the cached one; null otherwise.</param>
/// <param name="Strip">The summary strip, one part per shown panel that has one, in panel order.</param>
/// <param name="Panels">The panels in the settings' order; a panel the settings leave out is absent.</param>
/// <param name="Diff">The diff, the right pane.</param>
public sealed record ReviewPageModel(
    AsOf AsOf,
    ReviewPagePr Pr,
    ReviewPageSettings Settings,
    ReviewPageMoving? Moving,
    ReviewPageOffline? Offline,
    IReadOnlyList<ReviewPageStrip> Strip,
    IReadOnlyList<ReviewPagePanel> Panels,
    ReviewPageDiff Diff);
