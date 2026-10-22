namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>The brush resource keys of one state.</summary>
/// <param name="Text">The glyph and word.</param>
/// <param name="Soft">The tinted fill behind a footer.</param>
/// <param name="Minimap">The minimap rectangle.</param>
internal sealed record RunStateBrushes(string Text, string Soft, string Minimap);
