namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>What the review page shows (review page spec §6.3, §16).</summary>
internal enum ReviewPageState
{
    /// <summary>Reading the page: the strip and panels show their placeholders.</summary>
    Loading,

    /// <summary>The page, live or cached.</summary>
    Ready,

    /// <summary>"PR not found" (SND-REVIEW-404).</summary>
    NotFound,

    /// <summary>Sindri doesn't sync the repo (SND-REVIEW-004).</summary>
    NotSynced,

    /// <summary>review.login is unset (SND-REVIEW-007): the whole page is the list's login prompt.</summary>
    LoginPrompt,

    /// <summary>"Offline, and this PR isn't cached yet".</summary>
    OfflineUncached,

    /// <summary>Any other refusal or an unreachable Sindri.</summary>
    Failed,
}
