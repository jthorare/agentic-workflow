using Aw.Sindri.Ui.ViewModels.Context.Whole;
using Aw.Sindri.Ui.ViewModels.Run;

namespace Aw.Sindri.Ui.ViewModels.Context.Hits;

/// <summary>The hits list beside the map while a search is on: one row per hit, best first, and how many there were in all.</summary>
internal sealed class MapHitsViewModel : ObservableObject
{
    /// <summary>Raised when a row is chosen (a click, Enter or an arrow key on it).</summary>
    internal event EventHandler<ValueEventArgs<MapSearchHit>>? Chosen;

    /// <summary>The list's title.</summary>
    public string Title => throw new NotSupportedException();

    /// <summary>The rows, best first.</summary>
    public IReadOnlyList<MapHitRow> Rows => throw new NotSupportedException();

    /// <summary>Whether the list shows: a search found something.</summary>
    public bool IsVisible => throw new NotSupportedException();

    /// <summary>"50 of 132" when there were more matches than rows; empty otherwise.</summary>
    public string CountText => throw new NotSupportedException();

    /// <summary>Whether <see cref="CountText"/> has words to show.</summary>
    public bool HasCount => throw new NotSupportedException();

    /// <summary>The row chosen last; null when none.</summary>
    public MapHitRow? Selected
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <summary>A search answered.</summary>
    /// <param name="hits">The hits, best first.</param>
    /// <param name="total">How many items matched in all.</param>
    internal void Show(IReadOnlyList<MapSearchHit> hits, int total) => throw new NotSupportedException();

    /// <summary>The search ended: the list goes.</summary>
    internal void Clear() => throw new NotSupportedException();

    /// <summary>A row was chosen.</summary>
    /// <param name="row">The row.</param>
    internal void Choose(MapHitRow row) => throw new NotSupportedException();
}
