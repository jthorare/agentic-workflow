namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>The concept layer asks the view to move focus to a pin button.</summary>
/// <param name="target">Which button.</param>
/// <param name="cardId">The tile's id for a card's button; null for the head's.</param>
internal sealed class PinFocusEventArgs(PinFocus target, string? cardId) : EventArgs
{
    /// <summary>Which button.</summary>
    public PinFocus Target { get; } = target;

    /// <summary>The tile's id for <see cref="PinFocus.PinThis"/> and <see cref="PinFocus.EditPin"/>.</summary>
    public string? CardId { get; } = cardId;
}
