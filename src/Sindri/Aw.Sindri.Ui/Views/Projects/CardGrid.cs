using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Aw.Sindri.Ui.Views.Projects;

#pragma warning disable MA0182 // why: ProjectsPage.axaml sets it, which the analyzer cannot see
/// <summary>
/// Keyboard use of a Projects card grid (projects page spec §3.3): the cards under the control are one tab stop (the one last focused, the first until then)
/// and the arrow keys move by card in reading order; Up and Down keep the column of the row as drawn, or take the last card of a shorter row. A key typed
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
        if (sender is not Control grid || e.KeyModifiers != KeyModifiers.None || e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down)
            || e.Source is not Visual source || source is TextBox || source.FindAncestorOfType<TextBox>() is not null
            || CardOf(source) is not { } from)
        {
            return;
        }

        var cards = Cards(grid);
        var target = Target(grid, cards, from, e.Key);
        if (target is not null && !ReferenceEquals(target, from))
        {
            _ = target.Focus(NavigationMethod.Directional);
        }

        e.Handled = true;
    }

    private static Border? Target(Control grid, List<Border> cards, Border from, Key key)
    {
        var at = cards.IndexOf(from);
        if (at < 0)
        {
            return null;
        }

        if (key is Key.Left or Key.Right)
        {
            return cards[Math.Clamp(at + (key == Key.Right ? 1 : -1), 0, cards.Count - 1)];
        }

        // Rows as drawn: cards that share a top edge, each row in left-to-right order.
        var rows = cards.Select(c => (Card: c, Pos: c.TranslatePoint(default, grid) ?? default))
            .GroupBy(p => Math.Round(p.Pos.Y, MidpointRounding.AwayFromZero))
            .OrderBy(g => g.Key)
            .Select(g => g.OrderBy(p => p.Pos.X).Select(p => p.Card).ToList())
            .ToList();
        var row = rows.FindIndex(r => r.Contains(from));
        var next = row + (key == Key.Down ? 1 : -1);
        if (row < 0 || next < 0 || next >= rows.Count)
        {
            return from;
        }

        return rows[next][Math.Min(rows[row].IndexOf(from), rows[next].Count - 1)];
    }
}
#pragma warning restore MA0182
