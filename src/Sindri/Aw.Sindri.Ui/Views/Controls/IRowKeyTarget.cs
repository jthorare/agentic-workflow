namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>An items control that also takes the list keys of <see cref="RowKeys"/> (j, k, Space, Enter, Esc) and holds entries that are not rows, such as group headers.</summary>
internal interface IRowKeyTarget
{
    /// <summary>True when the entry at <paramref name="index"/> is a row the cursor can stand on.</summary>
    /// <param name="index">The index in the list's items.</param>
    /// <returns>True for a row, false for a header.</returns>
    public bool IsRowIndex(int index);

    /// <summary>Scrolls the row at <paramref name="index"/> into view, clear of any sticky header the list draws over its top edge.</summary>
    /// <param name="index">The index in the list's items.</param>
    public void RevealRow(int index);
}
