using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

#pragma warning disable MA0182 // why: WorkflowsPage.axaml and WorkflowEditorSheet.axaml place it, which the analyzer cannot see
/// <summary>The selected step's validation findings (#3803). Fix runs the page's fix, then puts the keyboard in the field the fix names, inside the step sheet.</summary>
internal sealed partial class WorkflowFindingsList : UserControl
{
    public WorkflowFindingsList()
    {
        InitializeComponent();
    }

#pragma warning disable MA0155 // why: an Avalonia RoutedEvent handler returns void, so it can only be async void
    private async void OnFix(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not WorkflowsPageViewModel page || (sender as Control)?.DataContext is not WorkflowFindingViewModel finding)
        {
            return;
        }

        await page.FixFinding(finding, CancellationToken.None).ConfigureAwait(true);
        if (page.IsFieldPickerAsked && this.FindAncestorOfType<WorkflowsPage>() is { } host)
        {
            var name = page.FocusFieldName;
            // The sheet's fields are made on its next layout pass: focus once they are there
            Dispatcher.UIThread.Post(() => FocusField(host, name), DispatcherPriority.Loaded);
        }
    }
#pragma warning restore MA0155

    // The field's text box in the step sheet: the visible one bound to the field the fix names (a field has a one-line and a many-line box, one of them shown).
    private static void FocusField(WorkflowsPage host, string name)
    {
        var box = host.GetVisualDescendants()
            .OfType<WorkflowEditorSheet>()
            .SelectMany(sheet => sheet.GetVisualDescendants().OfType<TextBox>())
            .FirstOrDefault(t => t.IsEffectivelyVisible && t.DataContext is ConfigFieldViewModel field && string.Equals(field.Name, name, StringComparison.Ordinal));
        if (box is null)
        {
            return;
        }

        _ = box.Focus(NavigationMethod.Tab);
        box.CaretIndex = box.Text?.Length ?? 0;
    }
}
#pragma warning restore MA0182
