namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>The collapsed body's height in row slots (<see cref="TodoPanelViewModel"/>, continued).</summary>
internal sealed partial class TodoPanelViewModel
{
    /// <summary>How many row-high slots the collapsed body takes: 5 (Show all shares the From elsewhere row), or 1 when the list is empty.</summary>
    public int CollapsedSlots => IsEmpty && !IsLoading && !IsExpanded && Error.Length == 0 ? 1 : 5;

    private void RaiseSlots() => OnChanged(nameof(CollapsedSlots));
}
