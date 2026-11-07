namespace Aw.Sindri.Ui.ViewModels.Linear;

/// <summary>One line of a description or comment as runs; a blank line has none.</summary>
/// <param name="Runs">The runs, in order.</param>
internal sealed record LinearTextLine(IReadOnlyList<LinearTextRun> Runs)
{
    /// <summary>The line as plain words (what a screen reader reads).</summary>
    public string Text => string.Concat(Runs.Select(r => r.Text));
}
