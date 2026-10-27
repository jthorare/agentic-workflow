using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Evaluation;

/// <summary>
/// One catalog question with its input type taken away: what scoring a stored input needs (the outputs, the threshold, the
/// time budget) and the way to build the prompt for a stored input ('judge eval').
/// </summary>
public sealed class ScoringQuestion
{
    private readonly Func<JsonNode?, JsonSerializerOptions, QuestionRef?> _ask;

    internal ScoringQuestion(IReadOnlyList<string> outputs, double threshold, TimeSpan timeBudget, Func<JsonNode?, JsonSerializerOptions, QuestionRef?> ask)
    {
        Outputs = outputs;
        Threshold = threshold;
        TimeBudget = timeBudget;
        _ask = ask;
    }

    /// <summary>The allowed decisions.</summary>
    public IReadOnlyList<string> Outputs { get; }

    /// <summary>The default confidence threshold.</summary>
    public double Threshold { get; }

    /// <summary>How long one provider may take.</summary>
    public TimeSpan TimeBudget { get; }

    /// <summary>What a provider is asked for one stored input.</summary>
    /// <param name="input">The stored input as JSON.</param>
    /// <param name="json">The package's AwJson options.</param>
    /// <returns>The question, or null when the input doesn't read as the question's input or fails its check.</returns>
    public QuestionRef? Ask(JsonNode? input, JsonSerializerOptions json) => _ask(input, json);
}
