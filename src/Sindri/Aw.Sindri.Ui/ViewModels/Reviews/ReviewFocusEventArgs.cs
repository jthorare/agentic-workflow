namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>Something closed and focus goes back to <see cref="Focus"/>.</summary>
/// <param name="focus">Where focus goes.</param>
internal sealed class ReviewFocusEventArgs(ReviewFocus focus) : EventArgs
{
    /// <summary>Where focus goes.</summary>
    public ReviewFocus Focus { get; } = focus;
}
