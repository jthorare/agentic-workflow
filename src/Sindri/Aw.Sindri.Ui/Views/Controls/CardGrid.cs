using Avalonia.Controls;

namespace Aw.Sindri.Ui.Views.Controls;

#pragma warning disable MA0182, IDE0060 // the red stub (#2905): unused until the System page places it, parameters until it is real

/// <summary>Cards in rows that fill the width (#2905). Stub: the tests come first.</summary>
internal sealed class CardGrid : Panel
{
    /// <summary>How many columns fit.</summary>
    /// <param name="width">The width to fill.</param>
    /// <param name="minColumn">The narrowest a column may be.</param>
    /// <param name="gap">The space between columns.</param>
    /// <returns>The column count.</returns>
    internal static int ColumnsFor(double width, double minColumn, double gap) => 1;

    /// <summary>One column's width when <paramref name="columns"/> columns fill <paramref name="width"/>.</summary>
    /// <param name="width">The width to fill.</param>
    /// <param name="columns">The column count.</param>
    /// <param name="gap">The space between columns.</param>
    /// <returns>The column width.</returns>
    internal static double ColumnWidth(double width, int columns, double gap) => 0;
}
