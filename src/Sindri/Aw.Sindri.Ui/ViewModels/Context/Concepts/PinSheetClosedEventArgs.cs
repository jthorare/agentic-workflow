namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>The Pin sheet went away.</summary>
/// <param name="written">True when a write went through (approved or waiting): the layer reads its pins again.</param>
internal sealed class PinSheetClosedEventArgs(bool written) : EventArgs
{
    /// <summary>Gets a value indicating whether a write went through.</summary>
    public bool Written { get; } = written;
}
