namespace Aw.Judge.Features.EvaluateQuestion;

/// <summary>What one eval scores with: the names the report carries, the question's threshold and the --collapse label.</summary>
/// <param name="Question">The question.</param>
/// <param name="Provider">The provider's wire name.</param>
/// <param name="Variant">The input variant.</param>
/// <param name="Threshold">The question's confidence threshold.</param>
/// <param name="Collapse">The label that stays; every other becomes 'stalled', or null for none.</param>
internal sealed record EvalSetup(string Question, string Provider, string Variant, double Threshold, string? Collapse);
