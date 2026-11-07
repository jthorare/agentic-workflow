using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.ViewModels.Linear;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>The Linear issue sheet in the shell (<see cref="ShellViewModel"/>, continued; Linear hub spec §3): a deep link, a Work → Linear row or strip row, or a review header chip opens it; Home and the tray say how many issues changed.</summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Linear issue sheet.</summary>
    public LinearIssueSheetViewModel IssueSheet { get; private set; }

    /// <summary>The write-access banner's Connect asked for Settings → Connections → Linear (the page itself is not built yet; whoever builds it maps this to opening it).</summary>
    internal event EventHandler? LinearConnectRequested;

    /// <summary>True when the sheet's subject is a Linear issue.</summary>
    public bool IsIssueSheet => SheetLink?.Kind == DeepLinkKind.Issue;

    /// <summary>Home's line ("3 new on your Linear issues"); null when none are new. It opens Work on the Linear view.</summary>
    public string? LinearNewLine => Linear.NewCount > 0 ? Labels.LinearNewLine(Linear.NewCount) : null;

    /// <summary>True when Home draws the Linear line.</summary>
    public bool HasLinearNewLine => LinearNewLine is not null;

    /// <summary>Home's Linear line: Work, on the Linear view.</summary>
    internal void OpenLinear()
    {
        Linear.Choose(linear: true);
        GoTo(PageId.Work);
    }

    [MemberNotNull(nameof(IssueSheet))]
    private void InitIssueSheet(ILauncher launcher)
    {
        IssueSheet = new LinearIssueSheetViewModel(_api, launcher, _clock);
        Health.Follow((url, token) => IsIssueSheet && IsSheetOpen ? IssueSheet.Refresh(url, token) : Task.CompletedTask);
        Linear.IssueRequested += (_, e) => OpenIssue(e.Key);
        ReviewPage.IssueRequested += (_, e) => OpenIssue(e.Key);
        IssueSheet.PrRequested += (_, e) => OpenReviewPage(e.Repo, e.Number);
        IssueSheet.ConnectRequested += (_, _) =>
        {
            LinearConnectRequested?.Invoke(this, EventArgs.Empty);
            // Settings has no Connections → Linear row yet (the write-access PR adds it, with the key paste and team ticks); until then Connect opens Settings at the top.
            GoTo(PageId.Settings);
        };
        IssueSheet.LeaveConfirmed += (_, e) => LeaveIssue(e.Value);
        Linear.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(LinearWorkViewModel.NewCount), StringComparison.Ordinal))
            {
                Changed(nameof(LinearNewLine), nameof(HasLinearNewLine));
                Reattend();   // the tray's tooltip carries the count
            }
        };
    }

    /// <summary>The sheet's close button and Back: unsaved edits on an open issue sheet ask before they go.</summary>
    /// <returns>True when the question is showing and the sheet stays open.</returns>
    internal bool AskBeforeClosingIssue() => IsIssueSheet && IsSheetOpen && IssueSheet.Leaving(next: null);

    private void OpenIssue(string key) => Navigate(new DeepLink(DeepLinkKind.Issue, key));

    // A link that would replace or close the open issue sheet while it has unsaved edits asks first; Discard follows the link.
    private bool AskBeforeLeavingFor(DeepLink link, bool rowSheet) =>
        IsIssueSheet && IsSheetOpen && IssueSheet.HasChanges
        && !(link.Kind == DeepLinkKind.Issue && string.Equals(link.Subject, IssueSheet.Key, StringComparison.Ordinal))
        && !(PageOnly(link.Kind) is not null && link.Kind != DeepLinkKind.ProjectAdd)
        && IssueSheet.Leaving(next: null, then: () => Navigate(link, rowSheet));

    private void LeaveIssue(string? next)
    {
        if (next is null)
        {
            CloseSheet();
        }
        else
        {
            OpenIssue(next);   // the sheet's own request for another issue; links go through AskBeforeLeavingFor
        }
    }

    // The issue part of opening the sheet (OnSheetOpened): the sheet names its issue at once and reads it.
    private void OnIssueSheetOpened(DeepLink link)
    {
        Changed(nameof(IsIssueSheet));
        if (link.Kind != DeepLinkKind.Issue || link.Subject is not { } key)
        {
            return;
        }

        IssueSheet.Show(key);
        if (Health.ApiUrl() is { } url)
        {
            _ = IssueSheet.Open(url, key, CancellationToken.None);
        }
    }
}
