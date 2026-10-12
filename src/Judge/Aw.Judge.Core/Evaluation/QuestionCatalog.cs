using System.Collections.Frozen;
using Aw.Judge.Core.Questions;

namespace Aw.Judge.Core.Evaluation;

/// <summary>The questions the judge answers, by name, and how to ask one by its name.</summary>
public static class QuestionCatalog
{
    /// <summary>Every question name.</summary>
    public static FrozenSet<string> Names { get; } = FrozenSet.Create(
        StringComparer.Ordinal,
        WakeGateQuestion.Name,
        BriefScopeQuestion.Name,
        AskCheckQuestion.Name,
        RuleCheckQuestion.Name,
        ResolutionCheckQuestion.Name,
        TurnProgressQuestion.Name,
        UiElementRepairQuestion.Name,
        VisualCritiqueQuestion.Name);

    /// <summary>Evaluates the named question.</summary>
    /// <param name="evaluator">The evaluator.</param>
    /// <param name="question">A name in <see cref="Names"/>.</param>
    /// <param name="rawInput">The input text.</param>
    /// <param name="context">The providers, the chain and the question's config.</param>
    /// <param name="cancellationToken">Cancels the evaluation.</param>
    /// <returns>The outcome.</returns>
    public static ValueTask<EvaluationOutcome> Ask(
        this QuestionEvaluator evaluator, string question, string rawInput, EvaluationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        return question switch
        {
            WakeGateQuestion.Name => evaluator.Evaluate(WakeGateQuestion.Definition, rawInput, context, cancellationToken),
            BriefScopeQuestion.Name => evaluator.Evaluate(BriefScopeQuestion.Definition, rawInput, context, cancellationToken),
            AskCheckQuestion.Name => evaluator.Evaluate(AskCheckQuestion.Definition, rawInput, context, cancellationToken),
            RuleCheckQuestion.Name => evaluator.Evaluate(RuleCheckQuestion.Definition, rawInput, context, cancellationToken),
            ResolutionCheckQuestion.Name => evaluator.Evaluate(ResolutionCheckQuestion.Definition, rawInput, context, cancellationToken),
            TurnProgressQuestion.Name => evaluator.Evaluate(TurnProgressQuestion.Definition, rawInput, context, cancellationToken),
            UiElementRepairQuestion.Name => evaluator.Evaluate(UiElementRepairQuestion.Definition, rawInput, context, cancellationToken),
            VisualCritiqueQuestion.Name => evaluator.Evaluate(VisualCritiqueQuestion.Definition, rawInput, context, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(question), question, "not a question in the catalog"),
        };
    }
}
