using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
/// <summary>The Workflows page, continued (#3304): the copy button of a failed run's fix.</summary>
internal sealed partial class WorkflowsPage
{
#pragma warning disable MA0155 // why: an Avalonia RoutedEvent handler returns void, so it can only be async void
    private async void OnCopyRunFix(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel { Run.Failure: { } failure })
        {
            await failure.CopyFix(CancellationToken.None).ConfigureAwait(true);
        }
    }
#pragma warning restore MA0155
}
#pragma warning restore MA0182
