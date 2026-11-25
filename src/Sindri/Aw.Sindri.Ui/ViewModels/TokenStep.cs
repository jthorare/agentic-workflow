namespace Aw.Sindri.Ui.ViewModels;

/// <summary>One line of a tile's token steps: a numbered step, or a detail under one (a permission GitHub's token needs, shown muted and indented).</summary>
/// <param name="Text">The words.</param>
/// <param name="IsDetail">True for a line under a step.</param>
internal sealed record TokenStep(string Text, bool IsDetail = false);
