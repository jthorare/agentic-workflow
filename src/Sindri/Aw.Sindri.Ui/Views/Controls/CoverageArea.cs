namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>One area the strip reports on.</summary>
/// <param name="Name">The area's plain name.</param>
/// <param name="Level">How fully it was searched.</param>
/// <param name="LevelWord">The level in words.</param>
/// <param name="Detail">One short sentence of why, or null.</param>
internal sealed record CoverageArea(string Name, CoverageLevel Level, string LevelWord, string? Detail = null);
