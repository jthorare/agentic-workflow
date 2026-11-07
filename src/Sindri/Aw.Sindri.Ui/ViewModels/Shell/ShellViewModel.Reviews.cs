using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Reviews;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Reviews tab in the shell (<see cref="ShellViewModel"/>, continued; review page spec §3, §6.1): the ninth tab, its waiting-count badge and Home's waiting line.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Reviews list.</summary>
    public ReviewsPageViewModel Reviews { get; private set; }

    /// <summary>True on Reviews.</summary>
    public bool IsReviews => CurrentPage == PageId.Reviews;

    /// <summary>Home's line under the Needs-you rows ("2 PRs waiting on you"); null when none wait. It opens Reviews.</summary>
    public string? ReviewsWaitingLine { get; private set; }

    /// <summary>True when the sheet is the library errors sheet.</summary>
    public bool IsLibrarySheet => SheetLink?.Kind == DeepLinkKind.LibrarySheet;

    /// <summary>True when the Reviews header's library button opened the sheet, so Esc and Close return focus to it.</summary>
    internal bool OpenerLibrary { get; private set; }

    private void OnLibrarySheetOpened()
    {
        OpenerLibrary = false;   // the sheet's own opening clears it; the button's handler sets it right after
        Changed(nameof(IsLibrarySheet), nameof(SheetTitleIsMono));
    }

    /// <summary>Opens Reviews from Home's waiting line.</summary>
    internal void OpenReviews() => GoTo(PageId.Reviews);

    [MemberNotNull(nameof(Reviews))]
    private void InitReviews()
    {
        Reviews = new ReviewsPageViewModel(_api, _clock);
        Reviews.LibraryRequested += (_, _) =>
        {
            OpenSheet(DeepLink.LibrarySheet);
            OpenerLibrary = true;
        };
        Health.Follow(Reviews.Refresh);   // every read, whichever page is shown: the badge and Home's line need the count
        PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(IsReviews), StringComparison.Ordinal))
            {
                Reviews.SetShown(IsReviews);
            }
        };
        Reviews.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(ReviewsPageViewModel.WaitingCount), StringComparison.Ordinal))
            {
                ShowWaiting(Reviews.WaitingCount);
            }
        };
    }

    // The tab's badge and Home's line follow the waiting count: empty and gone at 0.
    private void ShowWaiting(int count)
    {
        Navigation.First(n => n.Page == PageId.Reviews).Count = count > 0 ? count.ToString(System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
        ReviewsWaitingLine = count > 0 ? Labels.ReviewsWaitingLine(count) : null;
        Changed(nameof(ReviewsWaitingLine), nameof(HasReviewsWaitingLine));
    }

    /// <summary>True when Home draws the waiting line.</summary>
    public bool HasReviewsWaitingLine => ReviewsWaitingLine is not null;
}
