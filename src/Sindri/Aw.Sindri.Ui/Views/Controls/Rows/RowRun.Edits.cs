using System.Collections.Specialized;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>The targeted edits of <see cref="RowRun"/>: one change of a group's rows becomes the same change in the run. Each returns false for a change it can't place.</summary>
internal sealed partial class RowRun
{
    private bool Insert(GroupState state, int first, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not { Count: > 0 } added || e.NewStartingIndex < 0 || e.NewStartingIndex > state.Visible)
        {
            return false;
        }

        var at = first + e.NewStartingIndex;
        foreach (var item in added.OfType<object>())
        {
            Entries.Insert(at++, item);
            state.Visible++;
        }

        return true;
    }

    private bool Remove(GroupState state, int first, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not { Count: > 0 } removed || e.OldStartingIndex < 0 || e.OldStartingIndex + removed.Count > state.Visible)
        {
            return false;
        }

        for (var k = 0; k < removed.Count; k++)
        {
            Entries.RemoveAt(first + e.OldStartingIndex);
        }

        state.Visible -= removed.Count;
        return true;
    }

    private bool Replace(GroupState state, int first, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not { Count: > 0 } now || e.OldItems?.Count != now.Count || e.NewStartingIndex < 0 || e.NewStartingIndex + now.Count > state.Visible)
        {
            return false;
        }

        var at = first + e.NewStartingIndex;
        foreach (var item in now.OfType<object>())
        {
            Entries[at++] = item;
        }

        return true;
    }

    private bool Move(GroupState state, int first, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not { Count: 1 } || e.OldStartingIndex < 0 || e.NewStartingIndex < 0 || Math.Max(e.OldStartingIndex, e.NewStartingIndex) >= state.Visible)
        {
            return false;
        }

        Entries.Move(first + e.OldStartingIndex, first + e.NewStartingIndex);
        return true;
    }
}
