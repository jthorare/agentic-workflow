using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>One line under "Needs a look": the problem as served and the glyph that goes with the health word.</summary>
/// <param name="Code">The SND code.</param>
/// <param name="Text">The problem in words.</param>
/// <param name="Glyph">The warning triangle while the health is degraded, the cross for stalled, not running and the rest.</param>
internal sealed record HealthProblemLine(string Code, string Text, StateGlyph Glyph)
{
    /// <summary>The Details button's label: the code sits behind it, not in the sentence (#3057).</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string DetailsText => Labels.FailedReadDetails;
#pragma warning restore CA1822, MA0041

    /// <summary>The line for a served problem under the given health word.</summary>
    /// <param name="problem">The served problem.</param>
    /// <param name="word">The health word ("degraded", "stalled", ...).</param>
    /// <returns>The line.</returns>
    internal static HealthProblemLine From(HealthProblemView problem, string? word) =>
        new(problem.Code, problem.Text, string.Equals(word, "degraded", StringComparison.Ordinal) ? StateGlyph.Warning : StateGlyph.Down);
}
