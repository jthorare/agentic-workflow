namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The map's accessible list (<see cref="WholeMapViewModel"/>, continued; UX pass C-3): what the canvas draws, as items a keyboard and a screen reader reach. Enter on one does what a click on the map does.</summary>
internal sealed partial class WholeMapViewModel
{
    private (WholeMapGeometry Source, IReadOnlyList<MapItemViewModel> Items)? _items;

    /// <summary>The list's name.</summary>
    public string ItemsName { get; } = Labels.ContextMapItems;

    /// <summary>What the canvas draws now, in reading order: the features of the overview, or the nodes.</summary>
    public IReadOnlyList<MapItemViewModel> Items
    {
        get
        {
            if (Shown is not { } shown)
            {
                return [];
            }

            if (_items is not { } cached || !ReferenceEquals(cached.Source, shown))
            {
                IReadOnlyList<MapItemViewModel> built = shown.ByArea
                    ? [.. shown.Tiles.Select(t => new MapItemViewModel(t.Key, t.Label, Labels.ContextMapItemName(t.Label, Labels.ContextWholeMembers(t.Members)), IsTile: true))]
                    : [.. shown.Nodes.Select(n => new MapItemViewModel(n.Key, n.Label, Labels.ContextMapItemName(n.Label, n.Kind), IsTile: false))];
                _items = cached = (shown, built);
            }

            return cached.Items;
        }
    }

    /// <summary>The item the pinned node or feature is, so the list follows the map.</summary>
    public MapItemViewModel? SelectedItem => Pinned is { } key ? Items.FirstOrDefault(i => string.Equals(i.Key, key, StringComparison.Ordinal)) : null;

    /// <summary>Enter or a click on an item: the same as a click on the map. A feature of the overview opens; a node is pinned, the camera centres on it and its panel opens.</summary>
    /// <param name="item">The item.</param>
    internal void ChooseItem(MapItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item.IsTile)
        {
            Pinned = item.Key;
            Refocus();
            OpenArea(item.Key);
        }
        else
        {
            Jump(item.Key);
        }
    }
}
