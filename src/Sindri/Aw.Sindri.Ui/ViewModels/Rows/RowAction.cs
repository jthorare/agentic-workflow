using System.Windows.Input;

namespace Aw.Sindri.Ui.ViewModels.Rows;

/// <summary>A button a row shows on hover and under the cursor.</summary>
/// <param name="Label">The button's text.</param>
/// <param name="Command">What it runs.</param>
internal sealed record RowAction(string Label, ICommand Command);
