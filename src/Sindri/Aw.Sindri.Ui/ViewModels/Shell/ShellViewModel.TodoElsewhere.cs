using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Reviews;
using Aw.Sindri.Ui.ViewModels.Todo;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Todo panel's other places in the shell (<see cref="ShellViewModel"/>, continued; todo spec §3, §5): the From elsewhere line, G T and the focus requests.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>Focus should move to a list on Home (G T, the palette's Todo row); the window honours it once Home is drawn.</summary>
    internal event EventHandler<HomeFocusRequestedEventArgs>? HomeFocusRequested;

    /// <summary>The From elsewhere line under the Todo panel's rows.</summary>
    public FromElsewhereViewModel Elsewhere { get; private set; }

    [MemberNotNull(nameof(Elsewhere))]
    private void InitElsewhere()
    {
        Elsewhere = new FromElsewhereViewModel(_api);
        Health.Follow(Elsewhere.Refresh);
        Todo.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(TodoPanelViewModel.OverdueCount), StringComparison.Ordinal) || string.Equals(e.PropertyName, nameof(TodoPanelViewModel.ToSortOutCount), StringComparison.Ordinal))
            {
                Reattend();   // the tray's tooltip carries the counts
            }
        };
    }

    // Reviews exists by now: its count and failure feed the line, and the line's links and Retry go to the shell.
    private void WireElsewhere()
    {
        Reviews.PropertyChanged += OnReviewsForElsewhere;
        FeedReviews();
        Health.Banner.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(ViewsBanner.IsStalled), StringComparison.Ordinal))
            {
                Elsewhere.SetViewsStalled(Health.Banner.IsStalled);   // Reviews and Linear read through the views: a stall is why they stay unanswered
            }
        };
        Elsewhere.SetViewsStalled(Health.Banner.IsStalled);
        Elsewhere.OpenRequested += (_, e) => OpenElsewhere(e.Part);
        Elsewhere.ConnectRequested += (_, _) => OpenLinearConnection();
        Elsewhere.ReviewsRetryRequested += (_, _) =>
        {
            if (Health.ApiUrl() is { } url)
            {
                _ = Reviews.Refresh(url, CancellationToken.None);
            }
        };
    }

    private void OnReviewsForElsewhere(object? sender, PropertyChangedEventArgs e)
    {
        string[] fed = [nameof(ReviewsPageViewModel.IsLoading), nameof(ReviewsPageViewModel.WaitingCount), nameof(ReviewsPageViewModel.Error)];
        if (fed.Contains(e.PropertyName, StringComparer.Ordinal))
        {
            FeedReviews();
        }
    }

    private void FeedReviews() => Elsewhere.SetReviews(Reviews.IsLoading, Reviews.WaitingCount, failed: Reviews.Error.Length > 0);

    private void OpenElsewhere(ElsewherePart part)
    {
        switch (part)
        {
            case ElsewherePart.Reviews:
                OpenReviews();
                break;
            case ElsewherePart.Linear:
                OpenLinear();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(part), part, message: null);
        }
    }

    /// <summary>Shows Home and asks for focus on the Todo panel's first row (G T, the palette's Todo row).</summary>
    internal void OpenTodo()
    {
        GoTo(PageId.Home);
        HomeFocusRequested?.Invoke(this, new HomeFocusRequestedEventArgs(HomeFocusTarget.Todo));
    }
}
