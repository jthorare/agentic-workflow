using System.Diagnostics.CodeAnalysis;
using Aw.Sindri.Ui.Navigation;
using Aw.Sindri.Ui.ViewModels.Jobs;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// The Jobs page and the job sheet in the shell (<see cref="ShellViewModel"/>, continued; API P6): the page reads while it is the page shown, a job link
/// or a row opens the sheet, and the sheet re-reads on every event while it is open. The page is not a tab: G J, the palette and System's link open it.
/// </summary>
internal sealed partial class ShellViewModel
{
    /// <summary>The Jobs page's list.</summary>
    public JobsPageViewModel Jobs { get; private set; }

    /// <summary>The job sheet.</summary>
    public JobSheetViewModel JobSheet { get; private set; }

    /// <summary>True on Jobs.</summary>
    public bool IsJobs => CurrentPage == PageId.Jobs;

    /// <summary>True when the sheet's subject is a job.</summary>
    public bool IsJobSheet => SheetLink?.Kind == DeepLinkKind.Job;

    [MemberNotNull(nameof(Jobs), nameof(JobSheet))]
    private void InitJobs()
    {
        Jobs = new JobsPageViewModel(_api, Health.Catalog, _clock);
        JobSheet = new JobSheetViewModel(_api);
        JobSheet.Wire(Health.Catalog, _clock);
        Health.Follow(Jobs.Refresh);
        Health.Follow((url, token) => IsJobSheet && IsSheetOpen ? JobSheet.Refresh(url, token) : Task.CompletedTask);
        PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(IsJobs), StringComparison.Ordinal))
            {
                Jobs.SetShown(IsJobs);
            }
        };
        Jobs.OpenRequested += (_, e) => OpenSheet(e.Link);
        JobSheet.PropertyChanged += (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(JobSheetViewModel.Heading), StringComparison.Ordinal))
            {
                Changed(nameof(SheetName), nameof(EdgeTabName));
            }
        };
    }

    // The job part of opening the sheet (OnSheetOpened): the sheet names its job at once and reads it.
    private void OnJobSheetOpened(DeepLink link)
    {
        Changed(nameof(IsJobSheet));
        if (link.Kind != DeepLinkKind.Job || link.Subject is not { } subject)
        {
            return;
        }

        JobSheet.Show(subject);
        if (Health.ApiUrl() is { } url)
        {
            _ = JobSheet.Open(url, subject, CancellationToken.None);
        }
    }

    // The job part of reopening the sheet (ReopenSheet): the job may have ended while the sheet was closed, so it is read again before it is trusted.
    private void OnJobSheetReopened()
    {
        if (IsJobSheet && Health.ApiUrl() is { } url)
        {
            _ = JobSheet.Refresh(url, CancellationToken.None);
        }
    }
}
