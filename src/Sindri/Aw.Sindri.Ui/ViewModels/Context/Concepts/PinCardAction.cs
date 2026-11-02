namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>The one button a pinned card's footer can carry (concept pins spec §3.7).</summary>
internal enum PinCardAction
{
    /// <summary>No button.</summary>
    None,

    /// <summary>Review and approve: the needs-you review the setup step uses.</summary>
    ReviewAndApprove,

    /// <summary>Open review: the Context page's held-map review.</summary>
    OpenReview,

    /// <summary>Try again: queues a map refresh.</summary>
    TryAgain,
}
