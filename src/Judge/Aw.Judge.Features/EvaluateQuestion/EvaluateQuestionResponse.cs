using System.Text.Json.Nodes;
using Aw.Judge.Core.Evals;

namespace Aw.Judge.Features.EvaluateQuestion;

/// <summary>The EvaluateQuestion response.</summary>
/// <param name="Report">The score.</param>
/// <param name="ReportPath">The report file written under judge/evals/.</param>
public sealed record EvaluateQuestionResponse(EvalReport Report, string ReportPath)
{
    /// <summary>The report as the CLI prints it and the .json report holds it.</summary>
    /// <returns>A new JSON object.</returns>
    public JsonObject ToJson() => EvalReportText.ToNode(Report);
}
