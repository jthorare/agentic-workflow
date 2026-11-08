namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The Needs-you inbox page (<see cref="NeedsYouPageViewModel"/>, continued): the card footer.</summary>
internal sealed partial class NeedsYouPageViewModel
{
    /// <summary>The card's footer, right: how long it has waited; empty when the row serves no time.</summary>
    public string FooterRight => Card?.Entry is { Since: var since } && since != default ? Labels.NeedsYouFootRight(Labels.NeedsYouWaited(_clock.GetUtcNow() - since)) : "";
}
