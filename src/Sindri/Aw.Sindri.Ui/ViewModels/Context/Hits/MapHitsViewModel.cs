using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.ViewModels.Context.Hits;

/// <summary>The hits list beside the map while a search is on: one row per hit, best first, and how many there were in all.</summary>
internal sealed class MapHitsViewModel : ObservableObject
{
    private int _total;

    /// <summary>Raised when a row is chosen (a click, Enter or an arrow key on it).</summary>
    internal event EventHandler<ValueEventArgs<MapSearchHit>>? Chosen;

    /// <summary>The list's title.</summary>
    public string Title { get; } = Labels.ContextHitsTitle;

    /// <summary>The rows, best first.</summary>
    public IReadOnlyList<MapHitRow> Rows { get; private set; } = [];

    /// <summary>Whether the list shows: a search found something.</summary>
    public bool IsVisible => Rows.Count > 0;

    /// <summary>"50 of 132" when there were more matches than rows; empty otherwise.</summary>
    public string CountText => _total > Rows.Count ? Labels.ContextHitsOf(Rows.Count, _total) : string.Empty;

    /// <summary>Whether <see cref="CountText"/> has words to show.</summary>
    public bool HasCount => CountText.Length > 0;

    /// <summary>The row chosen last; null when none. Setting it chooses the row (an arrow key moves it).</summary>
    public MapHitRow? Selected
    {
        get;
        set
        {
            if (!ReferenceEquals(field, value))
            {
                field = value;
                OnChanged(nameof(Selected));
                if (value is not null)
                {
                    Choose(value);
                }
            }
        }
    }

    /// <summary>A search answered.</summary>
    /// <param name="hits">The hits, best first.</param>
    /// <param name="total">How many items matched in all.</param>
    internal void Show(IReadOnlyList<MapSearchHit> hits, int total)
    {
        ArgumentNullException.ThrowIfNull(hits);
        Selected = null;
        (Rows, _total) = ([.. hits.Select(h => new MapHitRow(h))], total);
        Changed();
    }

    /// <summary>The search ended: the list goes.</summary>
    internal void Clear()
    {
        if (Rows.Count == 0 && Selected is null)
        {
            return;
        }

        Selected = null;
        (Rows, _total) = ([], 0);
        Changed();
    }

    /// <summary>A row was chosen.</summary>
    /// <param name="row">The row.</param>
    internal void Choose(MapHitRow row)
    {
        ArgumentNullException.ThrowIfNull(row);
        Chosen?.Invoke(this, new ValueEventArgs<MapSearchHit>(row.Hit));
    }

    private void Changed()
    {
        OnChanged(nameof(Rows));
        OnChanged(nameof(IsVisible));
        OnChanged(nameof(CountText));
        OnChanged(nameof(HasCount));
    }
}
