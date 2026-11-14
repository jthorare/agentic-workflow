using System.Collections.Specialized;
using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>How <see cref="RowList"/> keeps its flat run in line with the groups and their rows (#2978).</summary>
internal sealed partial class RowList
{
    // Syncs the run with the groups. Groups and rows that are still there keep their entries, so their containers and focus stay.
    private void Rebuild()
    {
        var cursor = BeginCursor(fallback: -1, lookupIndex: true);
        _outer?.CollectionChanged -= OnGroupsChanged;
        _outer = ItemsSource as INotifyCollectionChanged;
        if (_attached)
        {
            _outer?.CollectionChanged += OnGroupsChanged;
        }

        if (_run.Rebuild(ItemsSource, subscribe: _attached))
        {
            ((ItemsControl)this).ItemsSource = _run.Entries;
        }

        EndEdit(cursor);
    }

    private void OnGroupsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    // One group's rows changed: the run applies the same change to its entries. Focus stays on its row; if that row left, it goes to the row now at its place.
    private void OnItemsChanged(GroupState state, NotifyCollectionChangedEventArgs e)
    {
        var cursor = BeginCursor(fallback: -1, lookupIndex: false);
        var removedAt = _run.Apply(state, e);
        if (removedAt >= 0)
        {
            PendingFallback = removedAt;
        }

        EndEdit(cursor);
    }

    // A group folded or unfolded: its rows leave or return; the header stays.
    private void OnCollapsed(GroupState state)
    {
        var cursor = BeginCursor(fallback: _run.HeaderIndex(state), lookupIndex: false);
        _run.SyncGroup(state);
        EndEdit(cursor);
    }
}
