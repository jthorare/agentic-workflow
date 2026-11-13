namespace Aw.Sindri.Ui.ViewModels.Rows;

/// <summary>One fact column of a list: every row of the list shows the fact with this key at the same x.</summary>
/// <param name="Key">The key into <see cref="IRowItem.Facts"/>.</param>
/// <param name="Header">The column's name.</param>
/// <param name="Wide">True for the 136 px column, false for 104 px.</param>
internal sealed record RowColumn(string Key, string Header, bool Wide = false);
