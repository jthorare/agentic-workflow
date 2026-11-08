namespace Aw.Sindri.Ui.ViewModels.Todo;

/// <summary>A part of the From elsewhere line was pressed.</summary>
/// <param name="part">Which part.</param>
internal sealed class ElsewherePartEventArgs(ElsewherePart part) : EventArgs
{
    /// <summary>Which part.</summary>
    public ElsewherePart Part { get; } = part;
}
