namespace Aw.Sindri.Ui.ViewModels.Rows;

/// <summary>The "waits on" cell of a row: you (bold) or a name.</summary>
/// <param name="Who">Who it waits on.</param>
/// <param name="Name">The name to show when it is not the person.</param>
internal sealed record RowWaitsOn(RowWho Who, string Name);
