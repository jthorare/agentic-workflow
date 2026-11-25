namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>The collapsed body's height in row slots (<see cref="TodoPanelViewModel"/>, continued).</summary>
internal sealed partial class TodoPanelViewModel
{
    private const int CardSlots = CollapsedRows + 1;   // five rows and the Show all slot

    private int AttentionSlots { get; set; }

    /// <summary>How many row-high slots the collapsed card takes under its header, the From elsewhere line aside: 6 (five rows and the Show all slot), or 1 when the list is empty.</summary>
    public int CollapsedSlots => IsEmpty && !IsLoading && !IsExpanded && Error.Length == 0 ? 1 : CardSlots;

    /// <summary>True when the Show all slot has something to hold: the Show all button or the error line.</summary>
    public bool HasFootSlot => ShowsShowAll || Error.Length > 0;

    /// <summary>
    /// How many of the collapsed slots sit above the From elsewhere line: all 6 while loading or when the Show all slot holds something, else the
    /// slots the attention rows and the todo rows take (1 to 5), so the line sits right under the last todo (#2908).
    /// </summary>
    public int BodySlots => IsLoading || HasFootSlot || CollapsedSlots < CardSlots
        ? CollapsedSlots
        : Math.Clamp(AttentionSlots + Rows.Count, 1, CollapsedRows);

    /// <summary>The slots under the From elsewhere line that keep the card at one height (D13): what the body leaves of the collapsed slots.</summary>
    public int SpareSlots => CollapsedSlots - BodySlots;

    // The rows are patched: the attention rows take the first slots, the todo rows follow.
    private void TakeSlots(int attention)
    {
        AttentionSlots = attention;
        RaiseSlots();
    }

    private void RaiseSlots()
    {
        OnChanged(nameof(CollapsedSlots));
        OnChanged(nameof(HasFootSlot));
        OnChanged(nameof(BodySlots));
        OnChanged(nameof(SpareSlots));
    }
}
