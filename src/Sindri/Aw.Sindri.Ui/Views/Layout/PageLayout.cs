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

    /// <summary>Sets the page region's width class on a control (and so on everything under it).</summary>
    /// <param name="control">The page region.</param>
    /// <param name="tier">The class.</param>
    internal static void SetTier(Control control, PageTier tier) => control.SetValue(TierProperty, tier);

    /// <summary>What the Work table's header and rows need to lay their columns out: the table's width and which optional columns have anything in them.</summary>
    internal readonly record struct ColumnPlan(double Width, bool HasRepo, bool HasAction);

    /// <summary>The Work table's column plan, set by the page and read by its header and rows.</summary>
    internal static readonly AttachedProperty<ColumnPlan> PlanProperty =
        AvaloniaProperty.RegisterAttached<Control, Control, ColumnPlan>("Plan", new ColumnPlan(1200, HasRepo: true, HasAction: true), inherits: true);

    /// <summary>Reads the column plan under a control.</summary>
    /// <param name="control">The control.</param>
    /// <returns>The plan.</returns>
    internal static ColumnPlan GetPlan(Control control) => control.GetValue(PlanProperty);

    /// <summary>Sets the column plan on a control (and so on everything under it).</summary>
    /// <param name="control">The control.</param>
    /// <param name="plan">The plan.</param>
    internal static void SetPlan(Control control, ColumnPlan plan) => control.SetValue(PlanProperty, plan);

    /// <summary>
    /// The six column widths (title, repo, status, stage, age, action) for a table width; a 0 is a dropped column. A column with nothing in it is dropped first;
    /// then, only when the columns do not fit with the title's 160 px, Stage, then Repo, then the action column narrows. Age never goes.
    /// </summary>
    /// <param name="width">The table's width.</param>
    /// <param name="hasRepo">False when no shown row has a repo.</param>
    /// <param name="hasAction">False when no shown row has an action.</param>
    /// <returns>The widths, title column first (0 for the star).</returns>
    internal static double[] Widths(double width, bool hasRepo, bool hasAction)
    {
        double[] widths = [0, hasRepo ? 150 : 0, 130, 110, 70, hasAction ? 260 : 0];
        const double TitleMin = 160;
        if (TitleMin + widths.Sum() > width)
        {
            widths[3] = 0;
        }

        if (TitleMin + widths.Sum() > width)
        {
            widths[1] = 0;
        }

        if (TitleMin + widths.Sum() > width && widths[5] > 170)
        {
            widths[5] = 170;
        }

        return widths;
    }

    /// <summary>
    /// The Reviews table's six column widths (PR, author, age, CI, required checks, doc) for a table width; a 0 is a dropped column. Doc goes first, then
    /// Required, then CI, so a narrow page (a sheet open beside it) keeps the first three facts and the PR title keeps 160 px.
    /// </summary>
    /// <param name="width">The table's width; 0 or less means not measured yet (every column).</param>
    /// <param name="peek">True while a sheet is open beside the list: the grammar keeps the first 3 facts (Author, CI, Required) and age, so Doc goes.</param>
    /// <returns>The widths, title column first (0 for the star).</returns>
    internal static double[] ReviewWidths(double width, bool peek = false)
    {
        _ = peek;   // red: the peek rule is not built yet
        double[] widths = [0, 140, 70, 120, 160, 140];
        if (width <= 0)
        {
            return widths;
        }

        foreach (var drop in new[] { 5, 4, 3 })
        {
            if (160 + widths.Sum() > width)
            {
                widths[drop] = 0;
            }
        }

        return widths;
    }

    /// <summary>The Reviews table's width, set by the Reviews page; its grids (classed "reviews-table": the header and every row) lay their columns out from it.</summary>
    internal static readonly AttachedProperty<double> ReviewsWidthProperty =
        AvaloniaProperty.RegisterAttached<Control, Control, double>("ReviewsWidth", 0, inherits: true);

    /// <summary>Sets the Reviews table's width on a control (and so on everything under it).</summary>
    /// <param name="control">The control.</param>
    /// <param name="width">The room the columns have.</param>
    internal static void SetReviewsWidth(Control control, double width) => control.SetValue(ReviewsWidthProperty, width);

    static PageLayout()
    {
        _ = ReviewsWidthProperty.Changed.AddClassHandler<Grid>((grid, e) =>
        {
            if (grid.Classes.Contains("reviews-table") && e.NewValue is double width)
            {
                ApplyReviews(grid, width);
            }
        });
    }

    /// <summary>Sets a Reviews grid's columns for a table width; a dropped column's cells are hidden.</summary>
    /// <param name="table">The grid with the six Reviews columns.</param>
    /// <param name="width">The table's width.</param>
    internal static void ApplyReviews(Grid table, double width)
    {
        var widths = ReviewWidths(width);
        for (var i = 1; i < table.ColumnDefinitions.Count; i++)
        {
            table.ColumnDefinitions[i].Width = new GridLength(widths[i]);
        }

        foreach (var cell in table.Children.Where(c => Grid.GetColumn(c) is >= 1 and <= 5))
        {
            cell.IsVisible = widths[Grid.GetColumn(cell)] > 0;
        }
    }

    /// <summary>The class a page region of this width has.</summary>
    /// <param name="width">The page region's width.</param>
    /// <returns>The tier.</returns>
    internal static PageTier For(double width) =>
        width >= 1120 ? PageTier.Wide : width >= 1000 ? PageTier.Mid : width >= 760 ? PageTier.Narrow : PageTier.Tight;

    /// <summary>Sets a table's columns (the queue's header and rows) from the plan; the title column (0) stays a star, and a dropped column's cells are hidden.</summary>
    /// <param name="table">The grid with the six queue columns.</param>
    /// <param name="plan">The plan.</param>
    internal static void Apply(Grid table, ColumnPlan plan)
    {
        var widths = Widths(plan.Width, plan.HasRepo, plan.HasAction);
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
