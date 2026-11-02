using System.Text;
using Aw.Judge.Core.Config;
using Aw.Judge.Core.Questions;
using Aw.Kernel.Json;

namespace Aw.Judge.Core.Evaluation;

/// <summary>choose's entry: its definition comes from the input, so the input is read once to build it.</summary>
public sealed partial class QuestionEvaluator
{
    /// <summary>Evaluates a choose call: the definition is built from the call's own choices, criteria and threshold.</summary>
    /// <param name="rawInput">The input text as read; input that doesn't deserialize escalates as 'invalid-input'.</param>
    /// <param name="context">The providers, the chain and the question's config.</param>
    /// <param name="cancellationToken">Cancels the provider calls and the writes.</param>
    /// <returns>The decision, or an escalation.</returns>
    public ValueTask<EvaluationOutcome> EvaluateChoose(string rawInput, EvaluationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rawInput);
        ArgumentNullException.ThrowIfNull(context);
        var parsed = AwJson.Deserialize<ChooseInput>(Encoding.UTF8.GetBytes(rawInput), json);
        var question = parsed.IsError ? ChooseQuestion.Unreadable : ChooseQuestion.For(parsed.Value, context.InputCapChars ?? ProvidersConfig.DefaultInputCapChars);
        return Evaluate(question, rawInput, context, cancellationToken);
    }
}
