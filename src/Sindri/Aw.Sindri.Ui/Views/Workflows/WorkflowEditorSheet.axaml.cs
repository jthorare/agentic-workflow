using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
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
            var field = (TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() as Control)?.DataContext as ConfigFieldViewModel;
            await page.Save(CancellationToken.None).ConfigureAwait(true);
            if (field is not null)
            {
                FocusField(field.Name);   // the form is built again from the saved revision: focus goes back to the field it was on (#3303)
            }
        }
    }

    private void FocusField(string name) =>
        Dispatcher.UIThread.Post(
            () => _ = this.GetVisualDescendants().OfType<Control>().FirstOrDefault(c => c is TextBox or CheckBox or ComboBox && c.DataContext is ConfigFieldViewModel f && string.Equals(f.Name, name, StringComparison.Ordinal) && c.Focusable && c.IsEffectivelyVisible)?.Focus(NavigationMethod.Tab),
            DispatcherPriority.Loaded);

    private async void OnReload(object? sender, RoutedEventArgs e)
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            await page.ReloadEditor(CancellationToken.None).ConfigureAwait(true);
        }
    }
#pragma warning restore MA0155

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        // Escape closes the sheet; handled here, in the page, so the shell's own Escape (it closes the shell sheet) never sees it
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None && DataContext is WorkflowsPageViewModel { IsEditing: true })
        {
            Close();
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();

    private void Close()
    {
        if (DataContext is WorkflowsPageViewModel page)
        {
            page.CloseEditor();
            this.FindAncestorOfType<WorkflowsPage>()?.FocusEdit();
        }
    }
}
#pragma warning restore MA0182
