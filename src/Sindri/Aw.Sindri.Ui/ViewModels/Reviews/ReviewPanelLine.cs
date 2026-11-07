namespace Aw.Sindri.Ui.ViewModels.Reviews;

/// <summary>One row of the panels that list plain lines: a test plan row, a CI run, a piece of evidence, a risky path or a thread.</summary>
/// <param name="Primary">The line's first words (the test, the run's name, the label, the path, "kim-dev · X.cs:12").</param>
/// <param name="Secondary">The second words (what it verifies, its state and sha, why it is risky, the thread's first comment).</param>
/// <param name="Url">Where Open goes (GitHub); null for none.</param>
/// <param name="Glyph">The state's glyph, never color alone.</param>
internal sealed record ReviewPanelLine(string Primary, string Secondary, string? Url, StateGlyph Glyph);
