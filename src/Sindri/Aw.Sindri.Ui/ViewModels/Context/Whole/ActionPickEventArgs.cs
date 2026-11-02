namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>Names the action row a focus request is about.</summary>
/// <param name="row">The row whose search box or Pick another button takes focus.</param>
internal sealed class ActionPickEventArgs(PageSheetActionViewModel row) : EventArgs
{
    /// <summary>Gets the row.</summary>
    public PageSheetActionViewModel Row { get; } = row;
}
