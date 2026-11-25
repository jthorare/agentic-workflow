namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>The collapsed body's height in row slots (<see cref="TodoPanelViewModel"/>, continued).</summary>
internal sealed partial class TodoPanelViewModel
{
    /// <summary>How many row-high slots the collapsed body takes: 1 when the list is empty, 6 (five rows and the Show all slot) while loading or when Show all or the error shows, else 5 (#2908).</summary>
    public int CollapsedSlots => IsEmpty && !IsLoading && !IsExpanded && Error.Length == 0 ? 1 : IsLoading || HasFootSlot ? 6 : 5;

    /// <summary>True when the collapsed body's Show all slot has something to hold: the Show all button or the error line.</summary>
    public bool HasFootSlot => ShowsShowAll || Error.Length > 0;

    private void RaiseSlots()
    {
        OnChanged(nameof(CollapsedSlots));
        OnChanged(nameof(HasFootSlot));
    }
}
