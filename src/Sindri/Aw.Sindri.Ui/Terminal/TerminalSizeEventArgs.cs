namespace Aw.Sindri.Ui.Terminal;

/// <summary>The columns and rows the pane's drawing area fits.</summary>
/// <param name="columns">Columns.</param>
/// <param name="rows">Rows.</param>
internal sealed class TerminalSizeEventArgs(int columns, int rows) : EventArgs
{
    /// <summary>Columns.</summary>
    internal int Columns { get; } = columns;

    /// <summary>Rows.</summary>
    internal int Rows { get; } = rows;
}
