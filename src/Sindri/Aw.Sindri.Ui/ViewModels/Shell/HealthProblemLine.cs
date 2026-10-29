using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>One line under "Needs a look": the problem as served and the glyph that goes with the health word.</summary>
/// <param name="Code">The SND code.</param>
/// <param name="Text">The problem in words.</param>
/// <param name="Glyph">The warning triangle while the health is degraded, the cross for stalled, not running and the rest.</param>
internal sealed record HealthProblemLine(string Code, string Text, StateGlyph Glyph)
{
    /// <summary>The line for a served problem under the given health word.</summary>
    /// <param name="problem">The served problem.</param>
    /// <param name="word">The health word ("degraded", "stalled", ...).</param>
    /// <returns>The line.</returns>
    internal static HealthProblemLine From(HealthProblemView problem, string? word) =>
        new(problem.Code, problem.Text, string.Equals(word, "degraded", StringComparison.Ordinal) ? StateGlyph.Warning : StateGlyph.Down);
}
