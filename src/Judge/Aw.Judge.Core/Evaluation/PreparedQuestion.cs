using System.Text.Json.Nodes;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Evaluation;

/// <summary>A question and one stored input made ready to ask a provider outside the chain: the reference with its prompt, the input, the time budget.</summary>
/// <param name="reference">The question with the prompt built for the input.</param>
/// <param name="input">The validated input as JSON.</param>
/// <param name="timeBudget">How long one provider may take.</param>
public sealed class PreparedQuestion(QuestionRef reference, JsonNode? input, TimeSpan timeBudget)
{
    /// <summary>The question with the prompt built for the input.</summary>
    public QuestionRef Reference { get; } = reference;

    /// <summary>The validated input as JSON.</summary>
    public JsonNode? Input { get; } = input;

    /// <summary>How long one provider may take.</summary>
    public TimeSpan TimeBudget { get; } = timeBudget;
}
