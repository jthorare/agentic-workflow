namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>
/// The one table that puts a kind of node in a band and gives it a look (q86; the ui owns the mapping, owner q95): records and decisions are the why;
/// features, screens, actions, things, flows and concepts are the product; code is code; any other kind goes in an Other row, so nothing is dropped.
/// </summary>
internal static class NodeBands
{
    private static readonly (string Kind, WholeBand Band, NodeKindStyle Style)[] _table =
    [
        ("record", WholeBand.Intent, new(ChipShape.Pill, KindMark.Square, "Brush.Violet")),
        ("decision", WholeBand.Intent, new(ChipShape.Pill, KindMark.Diamond, "Brush.Violet")),
        ("feature", WholeBand.Product, new(ChipShape.Card, KindMark.Dot, "Brush.Success")),
        ("surface", WholeBand.Product, new(ChipShape.Card, KindMark.Square, "Brush.Success")),
        ("action", WholeBand.Product, new(ChipShape.Card, KindMark.Triangle, "Brush.Success")),
        ("thing", WholeBand.Product, new(ChipShape.Card, KindMark.Diamond, "Brush.Success")),
        ("flow", WholeBand.Product, new(ChipShape.Card, KindMark.Chevron, "Brush.Success")),
        ("concept", WholeBand.Product, new(ChipShape.Card, KindMark.Bar, "Brush.Success")),
        ("code", WholeBand.Code, new(ChipShape.Plate, KindMark.Bar, "Brush.InkMuted")),
    ];

    private static readonly NodeKindStyle _otherStyle = new(ChipShape.Card, KindMark.None, "Brush.InkSubtle");

    /// <summary>Gets the kinds the table knows, in its order.</summary>
    internal static IReadOnlyList<string> Kinds { get; } = [.. _table.Select(t => t.Kind)];

    /// <summary>The band a kind sits in.</summary>
    /// <param name="kind">The node kind.</param>
    /// <returns>Its band; <see cref="WholeBand.Other"/> for a kind the table does not know.</returns>
    internal static WholeBand Of(string kind) => Find(kind)?.Band ?? WholeBand.Other;

    /// <summary>A kind's look.</summary>
    /// <param name="kind">The node kind.</param>
    /// <returns>Its style; a plain card for a kind the table does not know.</returns>
    internal static NodeKindStyle StyleOf(string kind) => Find(kind)?.Style ?? _otherStyle;

    /// <summary>A band's title.</summary>
    /// <param name="band">The band.</param>
    /// <returns>"Why", "Product", "Code" or "Other".</returns>
    internal static string Title(WholeBand band) => band switch
    {
        WholeBand.Intent => Labels.ContextWholeBandWhy,
        WholeBand.Product => Labels.ContextMapAreaProduct,
        WholeBand.Code => Labels.ContextMapAreaCode,
        WholeBand.Other => Labels.ContextWholeBandOther,
        _ => Labels.ContextWholeBandOther,
    };

    /// <summary>What a band holds, in words.</summary>
    /// <param name="kinds">The kinds in the band and their counts.</param>
    /// <returns>"3 records · 1 decision": biggest first, at most four; empty for none.</returns>
    internal static string Meta(IEnumerable<KeyValuePair<string, int>> kinds)
    {
        ArgumentNullException.ThrowIfNull(kinds);
        return string.Join(" · ", kinds.Where(k => k.Value > 0)
            .OrderByDescending(k => k.Value).ThenBy(k => k.Key, StringComparer.Ordinal)
            .Take(4).Select(k => Labels.ContextWholeKindCount(k.Key, k.Value)));
    }

    /// <summary>The kinds counted by band, each band's kinds biggest first.</summary>
    /// <param name="counts">The kinds and their counts.</param>
    /// <returns>The bands that have any, with their kinds.</returns>
    internal static IReadOnlyDictionary<WholeBand, IReadOnlyList<KeyValuePair<string, int>>> ByBand(IEnumerable<KeyValuePair<string, int>> counts)
    {
        ArgumentNullException.ThrowIfNull(counts);
        return counts.Where(c => c.Value > 0).GroupBy(c => Of(c.Key)).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<KeyValuePair<string, int>>)[.. g.OrderByDescending(c => c.Value).ThenBy(c => c.Key, StringComparer.Ordinal)]);
    }

    private static (string Kind, WholeBand Band, NodeKindStyle Style)? Find(string kind)
    {
        foreach (var row in _table)
        {
            if (string.Equals(row.Kind, kind, StringComparison.Ordinal))
            {
                return row;
            }
        }

        return null;
    }
}
