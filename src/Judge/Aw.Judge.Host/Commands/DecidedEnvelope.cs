namespace Aw.Judge.Host.Commands;

/// <summary>A decided question's stdout (exit 0; ask-check 'continue' exits 2); the question's declared extra fields are added beside these.</summary>
/// <param name="Decision">The decision.</param>
/// <param name="Confidence">The confidence, 0 to 1.</param>
/// <param name="Model">The provider that decided ('rules' for a rule).</param>
/// <param name="ReasonCode">Why, e.g. 'pre-rule', 'model' or 'fallback-after-undecided'.</param>
/// <param name="Id">The decision's id (for why, undo, approve).</param>
internal sealed record DecidedEnvelope(string Decision, double Confidence, string Model, string ReasonCode, string Id);
