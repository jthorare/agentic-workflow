using System.Collections.Specialized;
using System.ComponentModel;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Rows;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>What a <see cref="RowRun"/> tracks for one group: its header entry, how many of its rows are in the run, and its subscriptions (held only while the list is on screen).</summary>
/// <param name="group">The group.</param>
/// <param name="onItems">Called when the group's rows change.</param>
/// <param name="onCollapsed">Called when the group folds or unfolds.</param>
internal sealed class GroupState(IRowGroup group, Action<GroupState, NotifyCollectionChangedEventArgs> onItems, Action<GroupState> onCollapsed)
{
    private bool _subscribed;

    /// <summary>The group.</summary>
    public IRowGroup Group { get; } = group;

    /// <summary>The header's entry in the run.</summary>
    public GroupEntry Entry { get; } = new(group);

    /// <summary>How many of the group's rows are in the run now (none while it is folded).</summary>
    public int Visible { get; set; }

    /// <summary>True while the group is folded.</summary>
    public bool Collapsed => Group is IRowGroupHeading { IsCollapsed: true };

    /// <summary>Starts listening to the group's rows and fold state.</summary>
    public void Subscribe()
    {
        if (_subscribed)
        {
            return;
        }

        _subscribed = true;
        (Group.Items as INotifyCollectionChanged)?.CollectionChanged += OnItems;
        (Group as INotifyPropertyChanged)?.PropertyChanged += OnProperty;
    }

    /// <summary>Stops listening.</summary>
    public void Unsubscribe()
    {
        if (!_subscribed)
        {
            return;
        }

        _subscribed = false;
        (Group.Items as INotifyCollectionChanged)?.CollectionChanged -= OnItems;
        (Group as INotifyPropertyChanged)?.PropertyChanged -= OnProperty;
    }

    private void OnItems(object? sender, NotifyCollectionChangedEventArgs e) => onItems(this, e);

    private void OnProperty(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || string.Equals(e.PropertyName, nameof(IRowGroupHeading.IsCollapsed), StringComparison.Ordinal))
        {
            onCollapsed(this);
        }
    }
}
