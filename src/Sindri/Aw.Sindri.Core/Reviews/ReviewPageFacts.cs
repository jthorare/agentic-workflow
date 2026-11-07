using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Core.Reviews;

/// <summary>Everything the page is built from, read once on one head (PR review page spec §6.2).</summary>
/// <param name="AsOf">How fresh the views are.</param>
/// <param name="Repo">owner/name.</param>
/// <param name="Number">The PR number.</param>
/// <param name="Login">review.login.</param>
/// <param name="SelfLogin">The forge credential's own login; null when it could not be read.</param>
/// <param name="Pr">The header facts.</param>
/// <param name="Settings">The settings the PR got.</param>
/// <param name="Files">The files the PR changes.</param>
/// <param name="Threads">The review threads.</param>
/// <param name="Runs">The check runs on the head; a failed read when the forge sent none.</param>
/// <param name="Library">The library's facts.</param>
/// <param name="Evidence">The evidence found on disk.</param>
/// <param name="Ticked">The manual checks ticked on this head.</param>
/// <param name="Moving">Whether the head moved twice during the read.</param>
public sealed record ReviewPageFacts(
    AsOf AsOf,
    string Repo,
    int Number,
    string Login,
    string? SelfLogin,
    ReviewPagePrFacts Pr,
    ReviewPageSettingsFacts Settings,
    ReviewPageRead<PullFiles> Files,
    ReviewPageRead<IReadOnlyList<PullThread>> Threads,
    ReviewPageRead<IReadOnlyList<PullCheckRun>> Runs,
    ReviewPageLibraryFacts Library,
    ReviewPageRead<IReadOnlyList<ReviewPageEvidenceItem>> Evidence,
    IReadOnlySet<string> Ticked,
    bool Moving);
