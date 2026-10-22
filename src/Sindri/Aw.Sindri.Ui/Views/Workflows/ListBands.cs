using Avalonia;
using Avalonia.Controls;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>
/// The columns the Workflows list keeps at its width, handed down from the list to its header and every row as an inherited property
/// (the list measures itself; there are no container queries in Avalonia). A hidden column is 0 wide and its cells are hidden.
/// </summary>
internal static class ListBands
{
    /// <summary>The columns in force for the list and everything under it.</summary>
    public static readonly AttachedProperty<ListColumns> ColumnsProperty =
        AvaloniaProperty.RegisterAttached<Control, Control, ListColumns>("Columns", ListColumns.For(0), inherits: true);

    /// <summary>Sets the columns on the list (and so on everything under it).</summary>
    /// <param name="control">The list.</param>
    /// <param name="columns">The columns.</param>
    internal static void SetColumns(Control control, ListColumns columns) => control.SetValue(ColumnsProperty, columns);

    /// <summary>Sets a six-column grid (a header or a row) for the columns.</summary>
    /// <param name="grid">The grid; its children sit in columns 0 to 5.</param>
    /// <param name="columns">The columns.</param>
    internal static void Apply(Grid grid, ListColumns columns)
    {
        var definitions = new ColumnDefinitions();
        foreach (var size in columns.Sizes)
        {
            definitions.Add(new ColumnDefinition(new GridLength(size.Value, size.IsStar ? GridUnitType.Star : GridUnitType.Pixel)) { MinWidth = size.Min });
        }

        grid.ColumnDefinitions = definitions;
        foreach (var cell in grid.Children.Where(c => Grid.GetColumn(c) is not (ListColumns.Item or ListColumns.Status)))
        {
            cell.IsVisible = columns.Shows(Grid.GetColumn(cell));   // Item and Status always show, and their cells decide for themselves (the status badge hides until it has a status)
        }
    }
}
