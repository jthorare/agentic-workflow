using Avalonia.Controls;
using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Jobs;

namespace Aw.Sindri.Ui.Views.Jobs;

/// <summary>The Jobs page: a title click on a row opens the job's sheet.</summary>
internal sealed partial class JobsPage : UserControl
{
    public JobsPage()
    {
        InitializeComponent();
        AddHandler(JobRow.OpenEvent, OnOpen);
    }

#pragma warning disable MA0155 // why: an Avalonia RoutedEvent handler returns void, so it can only be async void
    private async void OnRetry(object? sender, RoutedEventArgs e)
    {
        if (DataContext is JobsPageViewModel page)
        {
            await page.Retry(CancellationToken.None).ConfigureAwait(true);
        }
    }
#pragma warning restore MA0155

    private void OnOpen(object? sender, RoutedEventArgs e)
    {
        if (e.Source is JobRow { Job: { } job } && DataContext is JobsPageViewModel page)
        {
            page.RequestOpen(job.Link);
        }
    }
}
