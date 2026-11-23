using Avalonia.Interactivity;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

#pragma warning disable MA0182 // why: ShellWindow.axaml places it in a Deferred template, which the analyzer cannot see
/// <summary>The Workflows page, continued (#3691): "Run revision N once" next to Make live.</summary>
internal sealed partial class WorkflowsPage
{
#pragma warning disable MA0155 // why: an Avalonia RoutedEvent handler returns void, so it can only be async void
    private async void OnRunRevisionOnce(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            await page.RunRevisionOnce(CancellationToken.None).ConfigureAwait(true);
        }
    }
#pragma warning restore MA0155
}
