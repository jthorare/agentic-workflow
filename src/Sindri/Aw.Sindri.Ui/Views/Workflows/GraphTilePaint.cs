namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>What a tile or pill is painted with in one state: resource keys the canvas resolves from the theme, never colours.</summary>
/// <param name="Fill">The fill brush key.</param>
/// <param name="Border">The border brush key.</param>
/// <param name="BorderThickness">The border's width in device-independent pixels.</param>
/// <param name="Dashed">Whether the border is dashed.</param>
/// <param name="Title">The brush key of the title text.</param>
/// <param name="Kind">The brush key of the secondary text.</param>
internal sealed record GraphTilePaint(string Fill, string Border, double BorderThickness, bool Dashed, string Title, string Kind);
