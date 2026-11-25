using Avalonia.Input;
using Avalonia.Threading;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>The Workflows page (<see cref="WorkflowsPage"/>, continued): focus after an undo.</summary>
internal sealed partial class WorkflowsPage
{
    private WorkflowsPageViewModel? _undoPage;

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _undoPage?.StepUndone -= OnStepUndone;
        _undoPage = DataContext as WorkflowsPageViewModel;
        _undoPage?.StepUndone += OnStepUndone;
    }

    // The reload after an undo rebuilds the sheet's fields and the graph, which drops the focused control, and the toast that held focus is gone: focus would fall to the page
    // and draw its ring around everything. It goes back to the sheet's field while the sheet is open, else to the graph, where the step is selected.
    private void OnStepUndone(object? sender, EventArgs e)
    {
        if (_undoPage is { IsSettingsOpen: true })
        {
            FocusEditorField();
            return;
        }

        Dispatcher.UIThread.Post(() => _ = (FirstTarget(RunsCanvas, static c => true) ?? RunsCanvas).Focus(NavigationMethod.Tab), DispatcherPriority.Loaded);
    }
}
