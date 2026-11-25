namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>The collapsed body's height in row slots (<see cref="TodoPanelViewModel"/>, continued).</summary>
internal sealed partial class TodoPanelViewModel
{
    /// <summary>How many row-high slots the collapsed panel takes under its header, the From elsewhere line aside: 6 (five rows and the Show all slot), or 1 when the list is empty.</summary>
    public int CollapsedSlots => IsEmpty && !IsLoading && !IsExpanded && Error.Length == 0 ? 1 : 6;

    /// <summary>True when the Show all slot has something to hold: the Show all button or the error line.</summary>
    public bool HasFootSlot => ShowsShowAll || Error.Length > 0;

    /// <summary>True when the Show all slot is kept but holds nothing: the From elsewhere line moves up into it and the spare row goes under the line, so the card keeps its height (#2908, D13).</summary>
    public bool HasSpareSlot => CollapsedSlots > 1 && !HasFootSlot;

    private void RaiseSlots()
    {
        OnChanged(nameof(CollapsedSlots));
        OnChanged(nameof(HasFootSlot));
        OnChanged(nameof(HasSpareSlot));
    }
}
