using Avalonia.Media;

namespace Aw.Sindri.Ui.Views.Controls.Rows;

/// <summary>One chip the flat Row drew: its word, its tone, and the fill and ink it used.</summary>
/// <param name="Text">The chip's word.</param>
/// <param name="Tone">The tone name: neutral, ok, attention, error, running, waiting or draft.</param>
/// <param name="Fill">The fill brush.</param>
/// <param name="Ink">The text brush.</param>
internal sealed record RowChip(string Text, string Tone, IBrush? Fill, IBrush? Ink);
