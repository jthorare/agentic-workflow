using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

#pragma warning disable MA0182 // why: WorkflowsPage.axaml places it, which the analyzer cannot see
/// <summary>The workflow editor sheet: Save sends the changed fields, Reload reads the workflow again, Close hides the sheet and returns focus to the Edit button.</summary>
internal sealed partial class WorkflowEditorSheet : UserControl
{
    public WorkflowEditorSheet()
    {
        InitializeComponent();
    }

#pragma warning disable MA0155 // why: an Avalonia RoutedEvent handler returns void, so it can only be async void
    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            await page.Save(CancellationToken.None).ConfigureAwait(true);
        }
    }

    private async void OnReload(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            await page.Editor.Reload(CancellationToken.None).ConfigureAwait(true);
        }
    }
#pragma warning restore MA0155

    private void OnClose(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            page.CloseEditor();
            this.FindAncestorOfType<WorkflowsPage>()?.FocusEdit();
        }
    }
}
#pragma warning restore MA0182
