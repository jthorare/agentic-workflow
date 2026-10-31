using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Concepts;

/// <summary>What opened the Pin sheet (concept pins spec §3.6): the concept layer hands one to the shell, which opens its sheet.</summary>
/// <param name="Repo">The repo the layer shows; empty for the profile's only repo.</param>
/// <param name="Card">Pin this: the derived card it was pressed on; null otherwise.</param>
/// <param name="Pin">Edit pin: the pin as GET /sindri/context/pins gave it; null otherwise.</param>
/// <param name="Pins">Every pin of the live profile (refused words, Add several's "Updates your pin" rows).</param>
/// <param name="Several">Add several rather than one pin.</param>
/// <param name="Concepts">The concept layer's cards, for Add several's matching; null reads as none.</param>
#pragma warning disable MA0182 // Q7b: the concept layer and the shell use it in the next commits
internal sealed record PinSheetOpen(string Repo, ConceptTileViewModel? Card, PinView? Pin, IReadOnlyList<PinView> Pins, bool Several = false, IReadOnlyList<ConceptTileViewModel>? Concepts = null)
{
    /// <summary>New pin: neither a card nor a pin.</summary>
    internal bool IsNew => Card is null && Pin is null && !Several;
}
#pragma warning restore MA0182
