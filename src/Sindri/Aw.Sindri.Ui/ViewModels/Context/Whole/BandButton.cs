namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One button of the row over a concept's map: it drills into a band.</summary>
/// <param name="Band">The band.</param>
/// <param name="Title">The band's title: "Why", "Product", "Code".</param>
/// <param name="Name">The button's accessible name: "Open the Why band".</param>
internal sealed record BandButton(WholeBand Band, string Title, string Name);
