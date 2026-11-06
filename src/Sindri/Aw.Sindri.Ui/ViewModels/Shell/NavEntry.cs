using Aw.Sindri.Ui.Navigation;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>
/// One segment of the top bar's navigation (UI spec §3.2). The entry keeps its identity when its count changes, so the
/// list's selection (the current page's highlight) is never dropped by a swap.
/// </summary>
internal sealed class NavEntry(PageId page, string label, string key, string count, string? shortLabel = null) : ObservableObject
{
    /// <summary>The page it opens.</summary>
    public PageId Page { get; } = page;

    /// <summary>Its label.</summary>
    public string Label { get; } = label;

    /// <summary>The label the top bar draws: its short label while the window is narrow and the tab has one, else <see cref="Label"/>. Screen-reader names keep the full label.</summary>
    public string DisplayLabel => UseShort && shortLabel is not null ? shortLabel : Label;

    /// <summary>True while the window is under 1,100 px wide (set by the top bar): tabs with a short label draw it.</summary>
    internal bool UseShort
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnChanged(nameof(DisplayLabel));
        }
    }

    /// <summary>The G-chord's second key (§8): H, W, Q, R, C, A, ..</summary>
    public string Key { get; } = key;

    /// <summary>The count from the page's view rows, or empty until its route is served.</summary>
    public string Count
    {
        get;
        internal set
        {
            if (field == value)
            {
                return;
            }

            field = value;
            OnChanged(nameof(Count));
            OnChanged(nameof(HasCount));
            OnChanged(nameof(AccessibleName));
        }
    } = count;

    /// <summary>The segment's screen-reader name with its count ("Reviews, 2 waiting on you").</summary>
    public string AccessibleName => Page == PageId.Reviews && HasCount ? Labels.ReviewsBadgeName(Label, Count) : Label;

    /// <summary>True when there is a count to draw; an empty one takes no room (nine tabs must fit at 1,000 px).</summary>
    public bool HasCount => Count.Length > 0;
}
