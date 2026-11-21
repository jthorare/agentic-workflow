using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>
/// The flat run a <see cref="RowList"/> shows (#2978): one <see cref="GroupEntry"/> per group header, then that group's rows. It is edited in place, the way
/// <see cref="StickyGroupList"/> keeps its blocks: an append is one insert, a removal one remove, and an entry that stays keeps its container and focus. A change the
/// run can't place is synced against the new rows with the fewest edits. A row matches an entry by <see cref="IRowItem.Key"/>, so a poll that hands over fresh
/// objects swaps data into the same container and doesn't rebuild it.
/// </summary>
internal sealed partial class RowRun(Action<GroupState, NotifyCollectionChangedEventArgs> onItems, Action<GroupState> onCollapsed)
{
    private readonly List<GroupState> _states = [];

    /// <summary>The entries the panel shows. Replaced only on a first fill or a swap to a list that shares nothing with the old one.</summary>
    public ObservableCollection<object> Entries { get; private set; } = [];

    /// <summary>The groups in order.</summary>
    public IReadOnlyList<GroupState> States => _states;

    /// <summary>The identity of a row or key: an <see cref="IRowItem.Key"/>, or the string itself; null for anything else.</summary>
    /// <param name="item">The row, key or header entry.</param>
    /// <returns>The key, or null.</returns>
    public static string? KeyOf(object? item) => item switch
    {
        IRowItem row => row.Key,
        string key => key,
        _ => null,
    };

    /// <summary>Brings the run in line with the groups.</summary>
    /// <param name="groups">The groups, each an <see cref="IRowGroup"/>.</param>
    /// <param name="subscribe">True while the list is on screen: follow each group's changes.</param>
    /// <returns>True when <see cref="Entries"/> is a new collection the list must show.</returns>
    public bool Rebuild(IEnumerable? groups, bool subscribe)
    {
        var old = new Dictionary<IRowGroup, GroupState>(ReferenceEqualityComparer.Instance);
        foreach (var state in _states)
        {
            _ = old.TryAdd(state.Group, state);
        }

        _states.Clear();
        var target = new List<object>();
        foreach (var group in groups?.OfType<IRowGroup>() ?? [])
        {
            var state = old.Remove(group, out var kept) ? kept : new GroupState(group, onItems, onCollapsed);
            _states.Add(state);
            Add(target, state, subscribe);
        }

        foreach (var gone in old.Values)
        {
            gone.Unsubscribe();
        }

        if (!ReplacesAll(target))
        {
            Sync(0, Entries.Count, target);
            return false;
        }

        Entries = new ObservableCollection<object>(target);   // a first fill or a new list: one swap beats thousands of inserts, and no entry stays
        return true;
    }

    /// <summary>Stops following every group.</summary>
    public void Unsubscribe()
    {
        foreach (var state in _states)
        {
            state.Unsubscribe();
        }
    }

    /// <summary>The index of a group's header in the run.</summary>
    /// <param name="state">The group.</param>
    /// <returns>The index.</returns>
    public int HeaderIndex(GroupState state)
    {
        var at = 0;
        foreach (var s in _states)
        {
            if (ReferenceEquals(s, state))
            {
                break;
            }

            at += 1 + s.Visible;
        }

        return at;
    }

    /// <summary>Applies one change of a group's rows to the run.</summary>
    /// <param name="state">The group.</param>
    /// <param name="e">The change.</param>
    /// <returns>For a removal, the run index the removed rows stood at (focus falls to the row there); otherwise -1.</returns>
    public int Apply(GroupState state, NotifyCollectionChangedEventArgs e)
    {
        if (state.Collapsed)
        {
            return -1;
        }

        var first = HeaderIndex(state) + 1;
        var placed = e.Action switch
        {
            NotifyCollectionChangedAction.Add => Insert(state, first, e),
            NotifyCollectionChangedAction.Remove => Remove(state, first, e),
            NotifyCollectionChangedAction.Replace => Replace(state, first, e),
            NotifyCollectionChangedAction.Move => Move(state, first, e),
            NotifyCollectionChangedAction.Reset => false,
            _ => false,
        };
        if (!placed || (state.Group.Items is ICollection all && all.Count != state.Visible))
        {
            SyncGroup(state);
        }

        return placed && e.Action == NotifyCollectionChangedAction.Remove ? first + e.OldStartingIndex : -1;
    }

    /// <summary>Shows or hides a group's rows after it folded or unfolded.</summary>
    /// <param name="state">The group.</param>
    public void SyncGroup(GroupState state)
    {
        var rows = RowsOf(state);
        Sync(HeaderIndex(state) + 1, state.Visible, rows);
        state.Visible = rows.Count;
    }

    private static void Add(List<object> target, GroupState state, bool subscribe)
    {
        if (subscribe)
        {
            state.Subscribe();
        }

        var rows = RowsOf(state);
        state.Visible = rows.Count;
        target.Add(state.Entry);
        target.AddRange(rows);
    }

    private static List<object> RowsOf(GroupState state) => state.Collapsed ? [] : [.. state.Group.Items.OfType<object>()];

    // True when the run is empty, or big and shares no group or row with the new one.
    private bool ReplacesAll(List<object> target)
    {
        if (Entries.Count == 0)
        {
            return true;
        }

        if (Entries.Count + target.Count < 512)
        {
            return false;
        }

        var (refs, keys) = Identities(Entries);
        return !target.Exists(t => refs.Contains(t) || (KeyOf(t) is { } k && keys.Contains(k)));
    }

    private static (HashSet<object> Refs, HashSet<string> Keys) Identities(IEnumerable<object> entries)
    {
        var refs = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            _ = refs.Add(entry);
            if (KeyOf(entry) is { } key)
            {
                _ = keys.Add(key);
            }
        }

        return (refs, keys);
    }

    // Makes Entries[start .. start+count) equal to target with the fewest edits: entries kept, a row with the same key as the one it replaces swapped in place, the rest removed, inserted or moved.
    private void Sync(int start, int count, List<object> target)
    {
        var (refs, keys) = Identities(target);
        for (var i = start + count - 1; i >= start; i--)
        {
            var entry = Entries[i];
            if (!refs.Contains(entry) && !(KeyOf(entry) is { } key && keys.Contains(key)))
            {
                Entries.RemoveAt(i);
                count--;
            }
        }

        var at = start;
        var end = start + count;
        foreach (var t in target)
        {
            if (at < end && ReferenceEquals(Entries[at], t))
            {
                at++;
                continue;
            }

            end += Place(at, end, t);
            at++;
        }
    }

    // Puts t at index at, looking for it (by reference or key) before end. Returns 1 when it had to be inserted.
    private int Place(int at, int end, object t)
    {
        var found = -1;
        for (var i = at; i < end; i++)
        {
            if (Same(Entries[i], t))
            {
                found = i;
                break;
            }
        }

        if (found < 0)
        {
            Entries.Insert(at, t);
            return 1;
        }

        if (found != at)
        {
            Entries.Move(found, at);
        }

        if (!ReferenceEquals(Entries[at], t))
        {
            Entries[at] = t;   // same key, a new object: that one container takes the new data
        }

        return 0;
    }

    private static bool Same(object a, object b) => ReferenceEquals(a, b) || (KeyOf(a) is { } ka && string.Equals(ka, KeyOf(b), StringComparison.Ordinal));
}
