using System.Collections.Frozen;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Aw.Judge.Core.Questions;
using Aw.Kernel.Json;

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
        VisualCritiqueQuestion.Name,
        ChooseQuestion.Name,
        ModelPickQuestion.Name);

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
            ChooseQuestion.Name => evaluator.EvaluateChoose(rawInput, context, cancellationToken),
            ModelPickQuestion.Name => evaluator.EvaluateModelPick(rawInput, context, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(question), question, "not a question in the catalog"),
        };
    }

    /// <summary>
    /// Reads a stored input for the named question and builds what a provider is asked, with no chain, rules or decision row
    /// (what 'judge adjudicate' needs). The input goes through the question's own record and check, like a live call's.
    /// </summary>
    /// <param name="question">A name in <see cref="Names"/>.</param>
    /// <param name="inputJson">The input text.</param>
    /// <param name="json">The package's AwJson options.</param>
    /// <returns>The prepared question, or null when the input doesn't read as the question's input (or a rule timed out).</returns>
    public static PreparedQuestion? Prepare(string question, string inputJson, JsonSerializerOptions json)
    {
        ArgumentNullException.ThrowIfNull(inputJson);
        ArgumentNullException.ThrowIfNull(json);
        return question switch
        {
            WakeGateQuestion.Name => Prepare(WakeGateQuestion.Definition, inputJson, json),
            BriefScopeQuestion.Name => Prepare(BriefScopeQuestion.Definition, inputJson, json),
            AskCheckQuestion.Name => Prepare(AskCheckQuestion.Definition, inputJson, json),
            RuleCheckQuestion.Name => Prepare(RuleCheckQuestion.Definition, inputJson, json),
            ResolutionCheckQuestion.Name => Prepare(ResolutionCheckQuestion.Definition, inputJson, json),
            TurnProgressQuestion.Name => Prepare(TurnProgressQuestion.Definition, inputJson, json),
            UiElementRepairQuestion.Name => Prepare(UiElementRepairQuestion.Definition, inputJson, json),
            VisualCritiqueQuestion.Name => Prepare(VisualCritiqueQuestion.Definition, inputJson, json),
            ChooseQuestion.Name => AwJson.Deserialize<ChooseInput>(Encoding.UTF8.GetBytes(inputJson), json) is { IsError: false } chosen
                ? Prepare(ChooseQuestion.For(chosen.Value), inputJson, json)
                : null,
            // why: model-pick's stored input holds no file text, so there is nothing to ask a provider again (adjudicate)
            ModelPickQuestion.Name => null,
            _ => throw new ArgumentOutOfRangeException(nameof(question), question, "not a question in the catalog"),
        };
    }

    private static PreparedQuestion? Prepare<TInput>(QuestionDefinition<TInput> question, string inputJson, JsonSerializerOptions json)
        where TInput : class
    {
        var parsed = AwJson.Deserialize<TInput>(Encoding.UTF8.GetBytes(inputJson), json);
        if (parsed.IsError)
        {
            return null;
        }

        try
        {
            return question.InputProblem?.Invoke(parsed.Value) is null
                ? new PreparedQuestion(question.ToRef(parsed.Value), JsonNode.Parse(AwJson.Serialize(parsed.Value, json)), question.TimeBudget)
                : null;
        }
        catch (RegexMatchTimeoutException)
        {
            return null;
        }
    }

    /// <summary>The named question as scoring reads it: a stored input in, the prompt out, with no chain around it.</summary>
    /// <param name="question">A question name.</param>
    /// <returns>The question, or null when the name isn't in <see cref="Names"/> or the question takes its outputs from each call (choose, model-pick).</returns>
    public static ScoringQuestion? Scoring(string question) => question switch
    {
        WakeGateQuestion.Name => Of(WakeGateQuestion.Definition),
        BriefScopeQuestion.Name => Of(BriefScopeQuestion.Definition),
        AskCheckQuestion.Name => Of(AskCheckQuestion.Definition),
        RuleCheckQuestion.Name => Of(RuleCheckQuestion.Definition),
        ResolutionCheckQuestion.Name => Of(ResolutionCheckQuestion.Definition),
        TurnProgressQuestion.Name => Of(TurnProgressQuestion.Definition),
        UiElementRepairQuestion.Name => Of(UiElementRepairQuestion.Definition),
        VisualCritiqueQuestion.Name => Of(VisualCritiqueQuestion.Definition),
        _ => null,
    };

    private static ScoringQuestion Of<TInput>(QuestionDefinition<TInput> definition)
        where TInput : class
        => new(definition.Outputs, definition.Threshold, definition.TimeBudget, (input, json) =>
        {
            var parsed = AwJson.Deserialize<TInput>(Encoding.UTF8.GetBytes(InputJson.Write(input, json)), json);
            if (parsed.IsError)
            {
                return null;
            }

            try
            {
                return definition.InputProblem?.Invoke(parsed.Value) is null ? definition.ToRef(parsed.Value) : null;
            }
            catch (RegexMatchTimeoutException)
            {
                return null;
            }
        });
}
