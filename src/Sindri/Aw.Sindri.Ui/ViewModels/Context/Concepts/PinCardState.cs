using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>What the pin side of a concept layer adds to one card: its mark, its footer and which pin buttons it carries.</summary>
/// <param name="Mark">"Set by a person", "Suggested" or empty (the unassigned concept, and every card while the server has no pins route).</param>
/// <param name="Pin">The pin the card is for; null for a derived card.</param>
/// <param name="Lines">The footer's lines.</param>
/// <param name="Action">The footer's button.</param>
/// <param name="ShowPinThis">Whether the card carries Pin this.</param>
/// <param name="ShowEditPin">Whether the card carries Edit pin.</param>
/// <param name="Waiting">Whether the card stands in for a pin the map has no node for yet.</param>
internal sealed record PinCardState(string Mark, PinView? Pin, IReadOnlyList<string> Lines, PinCardAction Action, bool ShowPinThis, bool ShowEditPin, bool Waiting)
{
    /// <summary>A card with no pin side.</summary>
    internal static PinCardState None { get; } = new(string.Empty, Pin: null, [], PinCardAction.None, ShowPinThis: false, ShowEditPin: false, Waiting: false);
}
