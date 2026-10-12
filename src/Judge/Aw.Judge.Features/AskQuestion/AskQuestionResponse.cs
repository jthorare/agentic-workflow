using Aw.Judge.Core.Evaluation;

namespace Aw.Judge.Features.AskQuestion;

/// <summary>The AskQuestion response.</summary>
/// <param name="Outcome">The decision or the escalation.</param>
/// <param name="ConfigWarning">Why the question ran on the default config instead of config.json, or null.</param>
public sealed record AskQuestionResponse(EvaluationOutcome Outcome, string? ConfigWarning);
