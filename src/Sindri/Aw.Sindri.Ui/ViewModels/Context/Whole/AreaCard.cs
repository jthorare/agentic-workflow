namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One area tile of the whole-repo overview (#3674): its name whole, what it holds, and its share of the repo.</summary>
/// <param name="Key">The area's key.</param>
/// <param name="Label">The area's name, never cut.</param>
/// <param name="Members">How many items the area holds.</param>
/// <param name="Counts">"1980 code files · 71 features · 96 records".</param>
/// <param name="Name">The accessible name.</param>
/// <param name="Share">The area's share of the items the areas hold, 0 to 1; the bar under the tile.</param>
/// <param name="Colour">The palette colour of the area, 0 to <see cref="WholePalette.Colours"/> - 1.</param>
internal sealed record AreaCard(string Key, string Label, int Members, string Counts, string Name, double Share, int Colour);
