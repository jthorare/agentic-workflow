using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Aw.Sindri.Ui.Views.Projects;

#pragma warning disable MA0182 // why: ProjectsPage.axaml sets it, which the analyzer cannot see
/// <summary>
/// Keyboard use of a Projects card grid (projects page spec §3.3): the cards under the control are one tab stop (the one last focused, the first until then)
/// and the arrow keys move by card in reading order; Up and Down keep the column of the row as drawn, or take the last card of a shorter row, and from the
/// last (first) row go on to the first (last) card of the next (previous) open fold. J and K step to the next and previous card across the page's grids,
/// Home and End go to its first and last card, PageUp and PageDown move a screen of rows within the grid. A key typed
/// into a text box, or with a modifier, is left alone, and so is Enter.
/// </summary>
internal static class CardGrid
{
    /// <summary>True on a control whose cards form one grid.</summary>
    public static readonly AttachedProperty<bool> IsGridProperty = AvaloniaProperty.RegisterAttached<Control, bool>("IsGrid", typeof(CardGrid));

    private static readonly AttachedProperty<Control?> _current = AvaloniaProperty.RegisterAttached<Control, Control?>("Current", typeof(CardGrid));

    static CardGrid()
    {
        _ = IsGridProperty.Changed.AddClassHandler<Control>((grid, e) =>
        {
            if (e.NewValue is true)
            {
                grid.AddHandler(InputElement.KeyDownEvent, OnKeyDown, RoutingStrategies.Bubble);
                grid.AddHandler(InputElement.GotFocusEvent, OnGotFocus, RoutingStrategies.Bubble, handledEventsToo: true);
                grid.LayoutUpdated += (_, _) => Roam(grid);
            }
        });
    }

    /// <summary>Reads <see cref="IsGridProperty"/>.</summary>
    /// <param name="element">The control.</param>
    /// <returns>True when it is a card grid.</returns>
    public static bool GetIsGrid(Control element) => element.GetValue(IsGridProperty);

    /// <summary>Sets <see cref="IsGridProperty"/>.</summary>
    /// <param name="element">The control.</param>
    /// <param name="value">True to make it a card grid.</param>
    public static void SetIsGrid(Control element, bool value) => element.SetValue(IsGridProperty, value);

    private static bool IsCard(Visual v) => v is Border b && b.Classes.Contains("pcard");

    private static List<Border> Cards(Control grid) =>
        [.. grid.GetVisualDescendants().OfType<Border>().Where(b => IsCard(b) && b.IsEffectivelyVisible)];

    private static Border? CardOf(Visual? source) => source?.GetSelfAndVisualAncestors().OfType<Border>().FirstOrDefault(IsCard);

    // Only the current card is a tab stop; the rest are reached by arrows.
    private static void Roam(Control grid)
    {
        var cards = Cards(grid);
        if (cards.Count == 0)
        {
            return;
        }

        var current = grid.GetValue(_current) is Border kept && cards.Contains(kept) ? kept : cards[0];
        foreach (var card in cards)
        {
            var stop = ReferenceEquals(card, current);
            if (card.IsTabStop != stop)
            {
                card.IsTabStop = stop;
            }
        }
    }

    private static void OnGotFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is Control grid && CardOf(e.Source as Visual) is { } card)
        {
            _ = grid.SetValue(_current, card);
            Roam(grid);
        }
    }

    private static void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not Control grid || e.KeyModifiers != KeyModifiers.None
            || e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down or Key.J or Key.K or Key.Home or Key.End or Key.PageUp or Key.PageDown)
            || e.Source is not Visual source || source is TextBox || source.FindAncestorOfType<TextBox>() is not null
            || CardOf(source) is not { } from)
        {
            return;
        }

        var target = Target(grid, from, e.Key);
        if (target is not null && !ReferenceEquals(target, from))
        {
            _ = target.Focus(NavigationMethod.Directional);
        }

        e.Handled = true;
    }

    // The grids of the page that show cards, top to bottom: the main grid, then an open fold's.
    private static List<Control> PageGrids(Control grid)
    {
        if (TopLevel.GetTopLevel(grid) is not Visual top)
        {
            return [grid];
        }

        var grids = top.GetVisualDescendants().OfType<Control>().Where(c => GetIsGrid(c) && c.IsEffectivelyVisible && Cards(c).Count > 0)
            .OrderBy(c => c.TranslatePoint(default, top)?.Y ?? 0).ToList();
        return grids.Contains(grid) ? grids : [grid];
    }

    // Rows as drawn: cards that share a top edge, each row in left-to-right order.
    private static List<List<Border>> Rows(Control grid, List<Border> cards) =>
        [.. cards.Select(c => (Card: c, Pos: c.TranslatePoint(default, grid) ?? default))
            .GroupBy(p => Math.Round(p.Pos.Y, MidpointRounding.AwayFromZero))
            .OrderBy(g => g.Key)
            .Select(g => g.OrderBy(p => p.Pos.X).Select(p => p.Card).ToList())];

    private static Border? Target(Control grid, Border from, Key key)
    {
        var cards = Cards(grid);
        var at = cards.IndexOf(from);
        if (at < 0)
        {
            return null;
        }

        if (key is Key.Left or Key.Right)
        {
            return cards[Math.Clamp(at + (key == Key.Right ? 1 : -1), 0, cards.Count - 1)];
        }

        return key is Key.J or Key.K or Key.Home or Key.End ? AcrossGrids(grid, from, key) : Vertical(grid, cards, from, key);
    }

    // J, K, Home and End walk the cards of every open grid on the page in reading order.
    private static Border AcrossGrids(Control grid, Border from, Key key)
    {
        var all = PageGrids(grid).SelectMany(Cards).ToList();
        if (key == Key.Home)
        {
            return all[0];
        }

        return key == Key.End ? all[^1] : all[Math.Clamp(all.IndexOf(from) + (key == Key.J ? 1 : -1), 0, all.Count - 1)];
    }

    private static Border Vertical(Control grid, List<Border> cards, Border from, Key key)
    {
        var rows = Rows(grid, cards);
        var row = rows.FindIndex(r => r.Contains(from));
        if (row < 0)
        {
            return from;
        }

        var paging = key is Key.PageDown or Key.PageUp;
        var step = paging ? PageRows(grid, rows) : 1;
        var next = row + (key is Key.Down or Key.PageDown ? step : -step);
        if (paging)
        {
            next = Math.Clamp(next, 0, rows.Count - 1);
        }
        else if (next < 0 || next >= rows.Count)
        {
            return Neighbour(grid, key == Key.Down) ?? from;
        }

        return rows[next][Math.Min(rows[row].IndexOf(from), rows[next].Count - 1)];
    }

    // The first card of the next grid down, or the last card of the previous grid up; null at the page's ends.
    private static Border? Neighbour(Control grid, bool down)
    {
        var grids = PageGrids(grid);
        var at = grids.IndexOf(grid);
        var other = at + (down ? 1 : -1);
        if (at < 0 || other < 0 || other >= grids.Count)
        {
            return null;
        }

        var cards = Cards(grids[other]);
        return down ? cards[0] : cards[^1];
    }

    // One screen of rows: the scroll viewer's height over the distance between two rows, at least one.
    private static int PageRows(Control grid, List<List<Border>> rows)
    {
        var viewport = grid.FindAncestorOfType<ScrollViewer>()?.Viewport.Height ?? grid.Bounds.Height;
        var first = rows[0][0];
        var pitch = rows.Count > 1
            ? (rows[1][0].TranslatePoint(default, grid)?.Y ?? 0) - (first.TranslatePoint(default, grid)?.Y ?? 0)
            : first.Bounds.Height;
        return pitch > 0 ? Math.Max(1, (int)(viewport / pitch)) : 1;
    }
}
#pragma warning restore MA0182
