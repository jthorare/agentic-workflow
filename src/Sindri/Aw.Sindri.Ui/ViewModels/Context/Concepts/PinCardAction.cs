namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

#pragma warning disable MA0182 // Q7b: the concept layer uses it in the next commit
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
#pragma warning restore MA0182
