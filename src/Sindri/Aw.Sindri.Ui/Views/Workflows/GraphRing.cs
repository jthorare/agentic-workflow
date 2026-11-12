namespace Aw.Sindri.Ui.Views.Workflows;

/// <summary>One ring drawn round a tile, outside its border.</summary>
/// <param name="Key">The brush key of the ring.</param>
/// <param name="Thickness">The ring's width in device-independent pixels.</param>
/// <param name="Gap">The space between the tile's edge and the ring.</param>
internal sealed record GraphRing(string Key, double Thickness, double Gap);
