using System.Collections.Specialized;
using Avalonia.Controls;
using Aw.Sindri.Ui.ViewModels.Shell;

namespace Aw.Sindri.Ui.Views.Shell;

/// <summary>Home's Needs-you card keeps its size honest as rows leave (<see cref="HomePage"/>, continued).</summary>
internal sealed partial class HomePage
{
    private ShellViewModel? _followed;

    // B-1: when a row leaves the Needs-you list the card is measured again, so its border and "all N" link sit under the rows that are left (never a height kept from before).
    private void FollowNeedsYouRows()
    {
        if (_followed is { } old)
        {
            old.NeedsYou.Rows.CollectionChanged -= OnNeedsYouRowsChanged;
        }

        _followed = DataContext as ShellViewModel;
        if (_followed is { } now)
        {
            now.NeedsYou.Rows.CollectionChanged += OnNeedsYouRowsChanged;
        }
    }

    private void OnNeedsYouRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action is NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace or NotifyCollectionChangedAction.Reset)
        {
            this.FindControl<ItemsControl>("NeedsYouRows")?.InvalidateMeasure();
            this.FindControl<Border>("NeedsYouPanel")?.InvalidateMeasure();
        }
    }
}
