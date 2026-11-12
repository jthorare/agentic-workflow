using System.Globalization;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>The words a screen reader hears for a count of rows (#2978): "1 row", "8 rows".</summary>
internal static class RowSpeech
{
    /// <summary>The count with its noun.</summary>
    /// <param name="count">How many rows.</param>
    /// <returns>"1 row" or "N rows".</returns>
    public static string Rows(int count) => count == 1 ? "1 row" : count.ToString(CultureInfo.InvariantCulture) + " rows";
}
