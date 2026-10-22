using Aw.Sindri.Ui.ViewModels.Context.Layout;

namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>Where every card sits and which sides its edges use.</summary>
/// <param name="Rects">Each card's rectangle in world coordinates, by card id.</param>
/// <param name="World">The union of the rectangles, padded; what Fit shows.</param>
/// <param name="Ports">The sides of each edge, by edge key.</param>
internal sealed record RunLayout(IReadOnlyDictionary<string, MapRect> Rects, MapRect World, IReadOnlyDictionary<string, EdgePorts> Ports);
