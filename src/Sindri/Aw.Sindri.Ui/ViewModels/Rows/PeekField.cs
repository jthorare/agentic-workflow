namespace Aw.Sindri.Ui.ViewModels.Rows;

/// <summary>One label and value of a peek pane's fields block.</summary>
/// <param name="Label">The label, in the dim ink.</param>
/// <param name="Value">The value.</param>
internal sealed record PeekField(string Label, string Value);
