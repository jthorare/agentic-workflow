using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Layout;

internal static partial class ContextMapLayout
{
    private const double IntentLeft = 14;   // where the intent band starts: the left edge of the first column, like the bands below it
    private const double CardRowStep = CardHeight + 14;

    // Every node's rectangle, band by band from the top: each band's height depends on how many rows the one above it wrapped to.
    private static Places Place(List<Entry> entries, List<LensEdge> edges)
    {
        var rects = new Dictionary<string, MapRect>(StringComparer.Ordinal);
        var near = Adjacency(edges);
        double Bary(Entry e) => near.TryGetValue(e.Key, out var others) && others.Where(rects.ContainsKey).Select(k => rects[k].Center.X).ToList() is { Count: > 0 } xs ? xs.Average() : double.MaxValue;
        List<Entry> Ordered(Row row) => [.. entries.Where(e => e.Row == row).OrderBy(Bary).ThenBy(e => e.Key, StringComparer.Ordinal)];

        var intentRows = Flow([.. entries.Where(e => e.Band == MapBand.Intent)], rects);
        var intentHeight = IntentHeight + ((Math.Max(intentRows, 1) - 1) * CardRowStep);
        var productTop = intentHeight + 52;

        var anchors = entries.Where(e => e.Row == Row.Features && e.IsAnchor);
        var features = anchors.Concat(Ordered(Row.Features).Where(e => !e.IsAnchor)).ToList();
        var extraFeatures = Features(features, productTop, rects);
        var surfaces = Ordered(Row.Surfaces);
        var surfaceRows = Grid(surfaces, productTop + 178 + extraFeatures, rects);
        var extraSurfaces = (Math.Max(surfaceRows, 1) - 1) * RowStep;
        var actions = Ordered(Row.Actions);
        var actionRows = Grid(actions, productTop + 264 + extraFeatures + extraSurfaces, rects);
        var extraActions = (Math.Max(actionRows, 1) - 1) * RowStep;
        var productHeight = ProductHeight + extraFeatures + extraSurfaces + extraActions;

        var codeTop = productTop + productHeight + 48;
        var files = Ordered(Row.Files);
        var fileRows = Grid(files, codeTop + 46, rects);
        var extraFiles = (Math.Max(fileRows, 1) - 1) * RowStep;
        var testRows = Grid(Ordered(Row.Tests), codeTop + 68 + extraFiles, rects);
        var codeHeight = CodeHeight + extraFiles + ((Math.Max(testRows, 1) - 1) * RowStep);

        StraddleShift([(Row.Surfaces, surfaces), (Row.Actions, actions)], entries, edges, rects);
        return new Places(rects, intentHeight, productTop, productHeight, codeTop, codeHeight);
    }

    private static Dictionary<string, List<string>> Adjacency(List<LensEdge> edges)
    {
        var near = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var e in edges)
        {
            (near.TryGetValue(e.From, out var a) ? a : near[e.From] = []).Add(e.To);
            (near.TryGetValue(e.To, out var b) ? b : near[e.To] = []).Add(e.From);
        }

        return near;
    }

    // The intent band is one flow, left to right from IntentLeft: tickets, then decisions. Returns how many rows it used.
    private static int Flow(List<Entry> items, Dictionary<string, MapRect> rects)
    {
        var (left, row, previous) = (IntentLeft, 0, (Entry?)null);
        foreach (var e in items)
        {
            var gap = Gap(previous, e);
            if (left + gap + e.Width > RightLimit && previous is not null)
            {
                (left, row, gap) = (IntentLeft, row + 1, 0);
            }

            left += gap;
            var cardTop = 52 + (row * CardRowStep);
            var top = e.Shape == MapShape.Card ? cardTop : cardTop + 36;
            rects[e.Key] = new MapRect(left, top, e.Width, e.Shape == MapShape.Card ? CardHeight : ChipHeight);
            (left, previous) = (left + e.Width, e);
        }

        return items.Count == 0 ? 0 : row + 1;
    }

    private static double Gap(Entry? previous, Entry next) =>
        previous is null ? 0
        : previous.Shape == MapShape.Card ? 80
        : next.Shape == MapShape.Card ? 60
        : 20;

    // The first product row: the anchor cards, then chips on the same columns as the rows below. Returns how much the product band grew.
    private static double Features(List<Entry> items, double productTop, Dictionary<string, MapRect> rects)
    {
        var anchored = items.Exists(e => e.IsAnchor);
        var (left, row, column, lastTop, started) = (254d, 0, 0, 66d, false);
        foreach (var e in items)
        {
            if (e.IsAnchor)
            {
                rects[e.Key] = new MapRect(left, productTop + 46, e.Width, CardHeight);
                left += e.Width + 20;
                continue;
            }

            if (!started)
            {
                (column, started) = (anchored ? FirstColumnAfter(left) : 0, true);
            }

            if (column >= Columns)
            {
                (row, column) = (row + 1, 0);
            }

            lastTop = row == 0 ? 66 : (anchored ? 184 : 66 + RowStep) + (RowStep * (row - 1));
            rects[e.Key] = new MapRect(FirstCentre + (Pitch * column) - (e.Width / 2), productTop + lastTop, e.Width, ChipHeight);
            column++;
        }

        return row == 0 ? 0 : Math.Max(178, lastTop + ChipHeight + 60) - 178;
    }

    // The first column whose chip (170 wide) starts at or after x.
    private static int FirstColumnAfter(double x) => (int)Math.Max(0, Math.Ceiling((x + 85 - FirstCentre) / Pitch));

    // A row of chips on the column grid, Columns to a row, RowStep between rows. Returns how many rows it used.
    private static int Grid(List<Entry> items, double top, Dictionary<string, MapRect> rects)
    {
        for (var i = 0; i < items.Count; i++)
        {
            var e = items[i];
            rects[e.Key] = new MapRect(FirstCentre + (Pitch * (i % Columns)) - (e.Width / 2), top + (RowStep * (i / Columns)), e.Width, ChipHeight);
        }

        return (items.Count + Columns - 1) / Columns;
    }

    // A straight link between two nodes that skips a row passes through any chip of that row standing on its x. The row moves half a
    // column over, so the link runs between two chips (only a single row of six or fewer has the room).
    private static void StraddleShift(List<(Row Row, List<Entry> Items)> rows, List<Entry> entries, List<LensEdge> edges, Dictionary<string, MapRect> rects)
    {
        var rank = entries.ToDictionary(e => e.Key, e => (int)e.Row, StringComparer.Ordinal);
        foreach (var (row, items) in rows)
        {
            if (items.Count is 0 or > Columns - 1)
            {
                continue;
            }

            var crossed = CrossedX(edges, rank, rects, (int)row);
            if (crossed.Any(x => items.Exists(i => rects[i.Key].Left - 8 <= x && x <= rects[i.Key].Right + 8)))
            {
                foreach (var i in items)
                {
                    rects[i.Key] = rects[i.Key].Translate(new MapVector(HalfColumn, 0));
                }
            }
        }
    }

    // The x of every vertical link that skips the row.
    private static IEnumerable<double> CrossedX(List<LensEdge> edges, Dictionary<string, int> rank, Dictionary<string, MapRect> rects, int row) =>
        edges.Where(e => rank.TryGetValue(e.From, out var from) && rank.TryGetValue(e.To, out var to) && Math.Min(from, to) < row && row < Math.Max(from, to)
                && rects.TryGetValue(e.From, out var a) && rects.TryGetValue(e.To, out var b) && Math.Abs(a.Center.X - b.Center.X) < 1)
            .Select(e => rects[e.From].Center.X);
}
