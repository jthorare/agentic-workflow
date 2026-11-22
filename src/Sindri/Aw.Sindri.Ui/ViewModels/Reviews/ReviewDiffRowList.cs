using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>
/// The diff's flat rows (#2776), changed a range at a time: a group that folds drops its rows in one Remove, a group that opens adds them in one Add.
/// The rows around keep their containers, so a focused group header stays focused and the scroll stays put.
/// </summary>
internal sealed class ReviewDiffRowList : ObservableCollection<ReviewDiffRow>
{
    /// <summary>Puts <paramref name="rows"/> in place of the <paramref name="count"/> rows from <paramref name="index"/>: one Remove, then one Add.</summary>
    /// <param name="index">The first row replaced.</param>
    /// <param name="count">How many rows go.</param>
    /// <param name="rows">The rows that come in.</param>
    internal void Replace(int index, int count, IReadOnlyList<ReviewDiffRow> rows)
    {
        CheckReentrancy();
        if (count > 0)
        {
            var removed = new List<ReviewDiffRow>(count);
            for (var i = 0; i < count; i++)
            {
                removed.Add(Items[index]);
                Items.RemoveAt(index);
            }

            Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removed, index));
        }

        if (rows.Count > 0)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                Items.Insert(index + i, rows[i]);
            }

            Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, rows.ToList(), index));
        }
    }

    /// <summary>All the rows, at once: one Reset.</summary>
    /// <param name="rows">The new rows.</param>
    internal void ResetTo(IReadOnlyList<ReviewDiffRow> rows)
    {
        CheckReentrancy();
        Items.Clear();
        foreach (var row in rows)
        {
            Items.Add(row);
        }

        Raise(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    private void Raise(NotifyCollectionChangedEventArgs change)
    {
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(change);
    }
}
