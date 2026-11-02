using System.Text;
using System.Text.Json;
using Aw.Judge.Core.Questions;
using Aw.Kernel.Json;

namespace Aw.Judge.Core.Labels;

/// <summary>
/// The questions a label can be given for: what each may answer and whether a stored input still fits its input record. The
/// 'choose' question is left out, because its outputs come from each call and no fixed set of labels exists.
/// </summary>
public static class LabelQuestions
{
    /// <summary>The label that leaves an item out of every score.</summary>
    public const string Skip = "skip";

    /// <summary>What a question may answer.</summary>
    /// <param name="question">The question's name.</param>
    /// <returns>The outputs, or null when the question can't be labelled (unknown, or choose).</returns>
    public static IReadOnlyList<string>? Outputs(string question) => question switch
    {
        WakeGateQuestion.Name => WakeGateQuestion.Definition.Outputs,
        BriefScopeQuestion.Name => BriefScopeQuestion.Definition.Outputs,
        AskCheckQuestion.Name => AskCheckQuestion.Definition.Outputs,
        RuleCheckQuestion.Name => RuleCheckQuestion.Definition.Outputs,
        ResolutionCheckQuestion.Name => ResolutionCheckQuestion.Definition.Outputs,
        TurnProgressQuestion.Name => TurnProgressQuestion.Definition.Outputs,
        UiElementRepairQuestion.Name => UiElementRepairQuestion.Definition.Outputs,
        VisualCritiqueQuestion.Name => VisualCritiqueQuestion.Definition.Outputs,
        _ => null,
    };

    /// <summary>Whether a stored input still reads as the question's input record and passes its check.</summary>
    /// <param name="question">The question's name.</param>
    /// <param name="inputJson">The stored input.</param>
    /// <param name="json">The package's AwJson options.</param>
    /// <returns>True when the question can be asked of the input.</returns>
    public static bool Fits(string question, string inputJson, JsonSerializerOptions json) => question switch
    {
        WakeGateQuestion.Name => Fit(WakeGateQuestion.Definition, inputJson, json),
        BriefScopeQuestion.Name => Fit(BriefScopeQuestion.Definition, inputJson, json),
        AskCheckQuestion.Name => Fit(AskCheckQuestion.Definition, inputJson, json),
        RuleCheckQuestion.Name => Fit(RuleCheckQuestion.Definition, inputJson, json),
        ResolutionCheckQuestion.Name => Fit(ResolutionCheckQuestion.Definition, inputJson, json),
        TurnProgressQuestion.Name => Fit(TurnProgressQuestion.Definition, inputJson, json),
        UiElementRepairQuestion.Name => Fit(UiElementRepairQuestion.Definition, inputJson, json),
        VisualCritiqueQuestion.Name => Fit(VisualCritiqueQuestion.Definition, inputJson, json),
        _ => false,
    };

    private static bool Fit<TInput>(QuestionDefinition<TInput> question, string inputJson, JsonSerializerOptions json)
        where TInput : class
    {
        var parsed = AwJson.Deserialize<TInput>(Encoding.UTF8.GetBytes(inputJson), json);
        return parsed.IsSuccess && question.InputProblem?.Invoke(parsed.Value) is null;
    }
}
