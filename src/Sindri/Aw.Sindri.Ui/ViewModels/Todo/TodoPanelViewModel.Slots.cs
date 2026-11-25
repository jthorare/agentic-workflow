namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>The collapsed body's height in row slots (<see cref="TodoPanelViewModel"/>, continued).</summary>
internal sealed partial class TodoPanelViewModel
{
    /// <summary>How many row-high slots the collapsed body takes: 6 (five rows and the Show all slot), or 1 when the list is empty.</summary>
    public int CollapsedSlots => IsEmpty && !IsLoading && !IsExpanded && Error.Length == 0 ? 1 : 6;

    /// <summary>True when the collapsed body keeps its Show all slot (it holds the error line too); false when the empty list takes one line.</summary>
    public bool HasFootSlot => CollapsedSlots > 1;

    private void RaiseSlots()
    {
        OnChanged(nameof(CollapsedSlots));
        OnChanged(nameof(HasFootSlot));
    }
}
