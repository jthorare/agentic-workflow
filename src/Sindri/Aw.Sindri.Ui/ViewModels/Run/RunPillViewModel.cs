namespace Aw.Sindri.Ui.ViewModels.Run;

/// <summary>A label pill on an edge of the run canvas, centred on the edge's midpoint (F3 spec 3 §2, "Pills").</summary>
/// <param name="Key">The edge's key.</param>
/// <param name="Text">The label.</param>
/// <param name="X">Centre x in the world.</param>
/// <param name="Y">Centre y in the world.</param>
/// <param name="Failed">Whether the pill's text is in the error tone.</param>
internal sealed record RunPillViewModel(string Key, string Text, double X, double Y, bool Failed);
