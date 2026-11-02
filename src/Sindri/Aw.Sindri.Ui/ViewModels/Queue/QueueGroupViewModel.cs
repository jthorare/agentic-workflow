using System.Collections.ObjectModel;
using System.Globalization;

namespace Aw.Sindri.Ui.ViewModels.Queue;

/// <summary>
/// One group of the queue (UI spec §4.5): its heading, its rows in the API's order, and its own empty sentence (E-18). A
/// refresh updates the group in place (<see cref="Update"/>), so a collapsed group stays collapsed and its rows' controls stay.
/// </summary>
internal sealed class QueueGroupViewModel : ObservableObject
{
    internal QueueGroupViewModel(string group)
    {
        Group = group;
        Label = Labels.Group(group);
        IsExpanded = true;
        Rows.CollectionChanged += (_, _) =>
        {
            OnChanged(nameof(Count));
            OnChanged(nameof(Header));
            OnChanged(nameof(IsVisible));
            OnChanged(nameof(ShowsEmpty));
        };
    }

    /// <summary>The API's group name.</summary>
    public string Group { get; }

    /// <summary>The group's heading.</summary>
    public string Label { get; }

    /// <summary>The heading with its count, as the section header reads ("Queued 4").</summary>
    public string Header => Label + " " + Count.ToString(CultureInfo.InvariantCulture);

    /// <summary>How many items the group holds.</summary>
    public int Count => Rows.Count;

    /// <summary>The rows, in the API's order.</summary>
    public ObservableCollection<ItemRowViewModel> Rows { get; } = [];

    /// <summary>The group's own sentence when it's empty, as served.</summary>
    public string? Empty { get; private set => Set(ref field, value); }

    /// <summary>
    /// True when the page shows the group: one with rows on the full queue, or the one the filter picked (§3.3), which
    /// shows its own sentence when empty. An empty group is hidden on the full queue (§4.5).
    /// </summary>
    public bool IsVisible => IsShown && (Rows.Count > 0 || IsFiltered);

    /// <summary>True when a filtered, empty group shows its own sentence.</summary>
    public bool ShowsEmpty => IsVisible && Rows.Count == 0;

    /// <summary>True when the section is open (collapsible, §4.5); rows show only then.</summary>
    public bool IsExpanded
    {
        get;
        set
        {
            Set(ref field, value);
            OnChanged(nameof(FoldGlyph));
        }
    }

    /// <summary>The fold arrow the header shows: down while open, right while folded.</summary>
    public string FoldGlyph => IsExpanded ? Labels.FoldOpen : Labels.FoldFolded;

    /// <summary>The filter allows this group.</summary>
    internal bool IsShown { get; private set; }

    /// <summary>The page is filtered to this one group.</summary>
    internal bool IsFiltered { get; private set; }

    /// <summary>Shows the group's current rows, sentence and visibility; <see cref="IsExpanded"/> is the human's and stays.</summary>
    /// <param name="rows">The rows to show, in order (instances already in the group are kept).</param>
    /// <param name="empty">The group's sentence as served.</param>
    /// <param name="shown">The filter allows the group.</param>
    /// <param name="filtered">The page is filtered to this group.</param>
    internal void Update(IReadOnlyList<ItemRowViewModel> rows, string? empty, bool shown, bool filtered)
    {
        Empty = empty;
        (IsShown, IsFiltered) = (shown, filtered);
        Patch.To(Rows, rows);
        OnChanged(nameof(IsVisible));
        OnChanged(nameof(ShowsEmpty));
    }
}
