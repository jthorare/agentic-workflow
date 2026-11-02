using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Aw.Sindri.Ui.ViewModels.Instance;
using Aw.Sindri.Ui.ViewModels.Shell;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Aw.Sindri.Ui.Views.Workflows;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>Focus when the instance frame closes (<see cref="ShellWindow"/>, continued; UI spec §11.2): Esc and Back return it to the Workflows row that opened the frame.</summary>
internal sealed partial class ShellWindow
{
    private PropertyChangedEventHandler? _awaitingFrame;

    // True when no row of the page (or strip, or queue) opened the sheet: a deep link, or a pointer.
    private static bool NoOpener(ShellViewModel shell) => shell is { OpenerWorkflow: null, OpenerStrip: null, OpenerRow: null };

    // The sheet opened on an instance: once the frame has an answer, focus goes to its first read action (Open in, else Copy status, else Back).
    private void EnterOpenedFrame(ShellViewModel shell)
    {
        var frame = shell.Instance;
        if (_awaitingFrame is not null)
        {
            frame.PropertyChanged -= _awaitingFrame;
            _awaitingFrame = null;
        }

        if (!shell.IsInstanceSheet)
        {
            return;
        }

        if (frame.Phase is not (FramePhase.Loading or FramePhase.Empty))
        {
            frame.FocusInitial();
            return;
        }

        _awaitingFrame = (_, e) =>
        {
            if (string.Equals(e.PropertyName, nameof(InstanceFrameViewModel.Phase), StringComparison.Ordinal) && frame.Phase is not (FramePhase.Loading or FramePhase.Empty))
            {
                frame.PropertyChanged -= _awaitingFrame;
                _awaitingFrame = null;
                if (shell.IsInstanceSheet)
                {
                    frame.FocusInitial();
                }
            }
        };
        frame.PropertyChanged += _awaitingFrame;
    }

    // An item's sheet opened from a frame: the control that had focus (the child's Open) left the tree, so focus goes to the sheet's heading.
    private void OnSheetStacked(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(() => this.Get<Border>("SheetTitle").Focus(Method()), DispatcherPriority.Loaded);

    // The sheet header's Back: the frame or sheet this one was opened from, else the sheet closes like Esc.
    private void OnBack(object? sender, RoutedEventArgs e) => Shell?.Back();

    // Where focus goes when the sheet closes: the strip row, the Workflows row or the queue row that opened it, the edge tab, else the row the selection moved to.
    private Control? SheetOpenerFrame() => OpenerStripFrame() ?? OpenerWorkflowFrame() ?? OpenerRecipeFrame() ?? OpenerSessionFrame() ?? OpenerProjectFrame() ?? OpenerRowFrame() ?? Usable(_sheetOpener) ?? SelectedRowFrame();

    // The Workflows group head whose Recipe button opened the sheet.
    private Control? OpenerRecipeFrame() => Shell?.OpenerRecipeGroup is { } recipe
        ? Usable(this.Get<Border>("Page").GetVisualDescendants().OfType<Button>()
            .FirstOrDefault(b => b.IsEffectivelyVisible && string.Equals(b.Name, "WorkflowGroupRecipe", StringComparison.Ordinal) && b.DataContext is RecipeGroupViewModel group && string.Equals(group.Recipe, recipe, StringComparison.Ordinal)))
        : null;

    // The list row's Open button, or in the board (the default view) the run card's frame.
    private Control? OpenerWorkflowFrame()
    {
        if (Shell?.OpenerWorkflow is not { } address)
        {
            return null;
        }

        var page = this.Get<Border>("Page");
        return Usable(page.GetVisualDescendants().OfType<InstanceRow>()
                .FirstOrDefault(r => r.IsEffectivelyVisible && string.Equals(r.Address, address, StringComparison.Ordinal))
                ?.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => string.Equals(b.Name, "WorkflowRowOpen", StringComparison.Ordinal)))
            ?? Usable(page.GetVisualDescendants().OfType<RunCard>()
                .FirstOrDefault(c => c.IsEffectivelyVisible && string.Equals(c.Address, address, StringComparison.Ordinal))
                ?.GetVisualDescendants().OfType<Border>().FirstOrDefault(b => string.Equals(b.Name, "WorkflowCardFrame", StringComparison.Ordinal)));
    }
}
