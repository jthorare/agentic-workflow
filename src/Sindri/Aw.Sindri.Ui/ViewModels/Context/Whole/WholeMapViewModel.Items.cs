namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The map's accessible list (<see cref="WholeMapViewModel"/>, continued; UX pass C-3): what the canvas draws (features or nodes, then their links), as items a keyboard and a screen reader reach. Enter on one does what a click on the map does.</summary>
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
                var built = BuildItems(shown);
                _items = cached = (shown, built);
            }

            return cached.Items;
        }
    }

    private static IReadOnlyList<MapItemViewModel> BuildItems(WholeMapGeometry shown)
    {
        if (shown.ByArea)
        {
            var tiles = shown.Tiles.ToDictionary(t => t.Key, t => t.Label, StringComparer.Ordinal);
            return
            [
                .. shown.Tiles.Select(t => new MapItemViewModel(t.Key, t.Label, Labels.ContextMapItemName(t.Label, Labels.ContextWholeMembers(t.Members)), IsTile: true)),
                .. shown.AreaEdges.Select(e => EdgeItem(e.From, e.To, e.Type, tiles, isTile: true)),
            ];
        }

        var nodes = shown.Nodes.ToDictionary(n => n.Key, n => n.Label, StringComparer.Ordinal);
        return
        [
            .. shown.Nodes.Select(n => new MapItemViewModel(n.Key, n.Label, Labels.ContextMapItemName(n.Label, n.Kind), IsTile: false)),
            .. shown.Edges.Select(e => EdgeItem(e.From, e.To, e.Type, nodes, isTile: false)),
        ];
    }

    // An edge is named for both its ends; its key is its own, so no node's pin selects it.
    private static MapItemViewModel EdgeItem(string from, string to, string type, Dictionary<string, string> labels, bool isTile)
    {
        var fromLabel = labels.GetValueOrDefault(from, from);
        var toLabel = labels.GetValueOrDefault(to, to);
        var name = Labels.ContextMapEdgeName(fromLabel, toLabel, type);
        return new MapItemViewModel($"edge:{from}>{to}:{type}", name, name, isTile, IsEdge: true, Target: from);
    }

    /// <summary>The item the pinned node or feature is, so the list follows the map.</summary>
    public MapItemViewModel? SelectedItem => Pinned is { } key ? Items.FirstOrDefault(i => string.Equals(i.Key, key, StringComparison.Ordinal)) : null;

    /// <summary>Enter or a click on an item: the same as a click on the map. A feature of the overview opens; a node is pinned, the camera centres on it and its panel opens.</summary>
    /// <param name="item">The item.</param>
    internal void ChooseItem(MapItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var key = item.IsEdge ? item.Target : item.Key;   // an edge goes to where it starts
        if (item.IsTile)
        {
            Pinned = key;
            Refocus();
            OpenArea(key);
        }
        else
        {
            Jump(key);
        }
    }
}
