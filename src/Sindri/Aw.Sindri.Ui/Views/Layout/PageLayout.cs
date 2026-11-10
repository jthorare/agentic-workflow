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

    /// <summary>Reads the page region's width class under a control.</summary>
    /// <param name="control">The control.</param>
    /// <returns>The class.</returns>
    internal static PageTier GetTier(Control control) => control.GetValue(TierProperty);

    /// <summary>Reads whether the Repo column shows under a control.</summary>
    /// <param name="control">The control.</param>
    /// <returns>True when it does.</returns>
    public static bool GetHasRepoColumn(Control control) => control.GetValue(HasRepoColumnProperty);

    /// <summary>Sets whether the Repo column shows under a control.</summary>
    /// <param name="control">The control.</param>
    /// <param name="value">True when it shows.</param>
    public static void SetHasRepoColumn(Control control, bool value) => control.SetValue(HasRepoColumnProperty, value);

    /// <summary>Sets the page region's width class on a control (and so on everything under it).</summary>
    /// <param name="control">The page region.</param>
    /// <param name="tier">The class.</param>
    internal static void SetTier(Control control, PageTier tier) => control.SetValue(TierProperty, tier);

    /// <summary>Whether the Repo column has anything to show; the queue sets it from its rows, and the rows and the header read it.</summary>
    public static readonly AttachedProperty<bool> HasRepoColumnProperty =
        AvaloniaProperty.RegisterAttached<Control, Control, bool>("HasRepoColumn", defaultValue: true, inherits: true);

    // Title, Repo, Status, Stage, Age, action. Narrower pages drop Stage, then Repo; Age never goes.
    private static readonly double[][] _widths =
    [
        [0, 150, 130, 110, 70, 260],
        [0, 150, 130, 0, 70, 260],
        [0, 0, 130, 0, 70, 260],
        [0, 0, 130, 0, 70, 170],
    ];

    /// <summary>The six column widths of a tier; a 0 is a dropped column.</summary>
    /// <param name="tier">The tier.</param>
    /// <param name="hasRepo">False when no shown row has a repo: the Repo column is dropped first.</param>
    /// <returns>The widths, title column first (0 for the star).</returns>
    internal static double[] Widths(PageTier tier, bool hasRepo)
    {
        var widths = (double[])_widths[(int)tier].Clone();
        if (!hasRepo)
        {
            widths[1] = 0;
        }

        return widths;
    }

    /// <summary>The six column widths for a table width. Red stub: the real rule follows.</summary>
    /// <param name="width">The table's width.</param>
    /// <param name="hasRepo">False when no shown row has a repo.</param>
    /// <param name="hasAction">False when no shown row has an action.</param>
    /// <returns>The widths.</returns>
    internal static double[] Widths(double width, bool hasRepo, bool hasAction) => Widths(For(width), hasRepo && hasAction);

    /// <summary>The class a page region of this width has.</summary>
    /// <param name="width">The page region's width.</param>
    /// <returns>The tier.</returns>
    internal static PageTier For(double width) =>
        width >= 1120 ? PageTier.Wide : width >= 1000 ? PageTier.Mid : width >= 760 ? PageTier.Narrow : PageTier.Tight;

    /// <summary>Sets a table's columns (the queue's header and rows) for a tier; the title column (0) stays a star, and a dropped column's cells are hidden.</summary>
    /// <param name="table">The grid with the six queue columns.</param>
    /// <param name="tier">The tier.</param>
    /// <param name="hasRepo">False when no shown row has a repo.</param>
    internal static void Apply(Grid table, PageTier tier, bool hasRepo)
    {
        var widths = Widths(tier, hasRepo);
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
