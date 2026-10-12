using System.Text.Json.Nodes;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Evaluation;

/// <summary>A provider or a rule decided.</summary>
/// <param name="Decision">The decision, one of the question's outputs.</param>
/// <param name="Confidence">The confidence, 0 to 1 (1 for a rule).</param>
/// <param name="Model">Who decided ('rules' for a pre-rule or a fallback rule).</param>
/// <param name="ReasonCode">Why.</param>
/// <param name="Id">The recorded decision's id (also returned when the write failed).</param>
public sealed record DecidedOutcome(string Decision, double Confidence, ProviderName Model, string ReasonCode, DecisionId Id)
    : EvaluationOutcome(ReasonCode)
{
    /// <summary>The provider's response fields beyond the decision, when it gave any.</summary>
    public JsonObject? Extra { get; init; }
}
