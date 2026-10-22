using Avalonia;
using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Layout;

/// <summary>
/// The page region's width class, an inherited attached property the shell sets on the page region from its bounds: the
/// page is the window minus a sheet beside it (WCAG 2.4.11: a sheet never covers page content), and the queue's rows and
/// header and Home read the class to drop low-priority columns, in a fixed order, so the title keeps 160 px (B7) and the
/// primary action stays in view. A view-side class; the view models know nothing of it.
/// </summary>
internal static class PageLayout
{
    /// <summary>The page region's width class.</summary>
    public static readonly AttachedProperty<PageTier> TierProperty =
        AvaloniaProperty.RegisterAttached<Control, Control, PageTier>("Tier", PageTier.Wide, inherits: true);

    /// <summary>Sets the page region's width class on a control (and so on everything under it).</summary>
    /// <param name="control">The page region.</param>
    /// <param name="tier">The class.</param>
    internal static void SetTier(Control control, PageTier tier) => control.SetValue(TierProperty, tier);

    private static readonly double[][] _widths =
    [
        [0, 150, 130, 110, 70, 260],
        [0, 150, 130, 0, 0, 260],
        [0, 0, 130, 0, 0, 260],
        [0, 0, 130, 0, 70, 170],
    ];

    /// <summary>The class a page region of this width has.</summary>
    /// <param name="width">The page region's width.</param>
    /// <returns>The tier.</returns>
    internal static PageTier For(double width) =>
        width >= 1120 ? PageTier.Wide : width >= 1000 ? PageTier.Mid : width >= 760 ? PageTier.Narrow : PageTier.Tight;

    /// <summary>Sets a table's columns (the queue's header and rows) for a tier; the title column (0) stays a star, and a dropped column's cells are hidden.</summary>
    /// <param name="table">The grid with the six queue columns.</param>
    /// <param name="tier">The tier.</param>
    internal static void Apply(Grid table, PageTier tier)
    {
        var widths = _widths[(int)tier];
        for (var i = 0; i < table.ColumnDefinitions.Count; i++)
        {
            table.ColumnDefinitions[i].Width = i == 0 ? new GridLength(1, GridUnitType.Star) : new GridLength(widths[i]);
        }

        foreach (var cell in table.Children.Where(c => Grid.GetColumn(c) is >= 1 and <= 4))
        {
            cell.IsVisible = widths[Grid.GetColumn(cell)] > 0;
        }
    }
}
