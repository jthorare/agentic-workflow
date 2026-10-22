using Avalonia.Controls;
using Aw.Sindri.Ui.Views.Layout;

namespace Aw.Sindri.Ui.Views.Recipes;

/// <summary>
/// The Recipes table's columns for a page width (recipes spec §2, narrow layout): the header and every row read the same widths, so they line up. Next to a sheet the page
/// is Narrow and drops what a person can read in the sheet (version, size, needs and produces); under 790 px only the workflow, its standing and its fingerprint stay.
/// </summary>
internal static class RecipeColumns
{
    // Workflow (a star), Version, Standing, Size, Needs and produces, Fingerprint, Runs, Finished, Typical time; 0 is a dropped column.
    private static readonly double[][] _widths =
    [
        [0, 70, 170, 80, 90, 100, 60, 90, 90],
        [0, 70, 170, 80, 90, 100, 60, 90, 90],
        [0, 0, 170, 0, 0, 100, 60, 90, 90],
        [0, 0, 170, 0, 0, 100, 0, 0, 0],
    ];

    /// <summary>The columns' widths for a tier; index 0 is the star column and reads 0.</summary>
    /// <param name="tier">The page width class.</param>
    /// <returns>Nine widths, 0 for a dropped column.</returns>
    internal static IReadOnlyList<double> Widths(PageTier tier) => _widths[(int)tier];

    /// <summary>Sets a grid's nine columns for a tier and hides the cells of a dropped column.</summary>
    /// <param name="grid">The header or a row's grid.</param>
    /// <param name="tier">The page width class.</param>
    internal static void Apply(Grid grid, PageTier tier)
    {
        var widths = _widths[(int)tier];
        for (var i = 0; i < grid.ColumnDefinitions.Count; i++)
        {
            grid.ColumnDefinitions[i].Width = i == 0 ? new GridLength(2, GridUnitType.Star) : new GridLength(widths[i]);
        }

        foreach (var cell in grid.Children.Where(c => Grid.GetColumn(c) > 0))
        {
            cell.IsVisible = widths[Grid.GetColumn(cell)] > 0;
        }
    }
}
