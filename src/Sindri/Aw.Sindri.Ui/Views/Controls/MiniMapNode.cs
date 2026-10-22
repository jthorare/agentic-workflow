using Avalonia;
using Avalonia.Media;

namespace Aw.Sindri.Ui.Views.Controls;

/// <summary>One rectangle of the minimap: a card's place in the world and the brush it is filled with.</summary>
/// <param name="Rect">The card's rectangle, in world coordinates.</param>
/// <param name="Fill">The fill, from the card's state.</param>
/// <param name="Selected">Whether the card is the selected one (outlined).</param>
internal sealed record MiniMapNode(Rect Rect, IBrush Fill, bool Selected);
