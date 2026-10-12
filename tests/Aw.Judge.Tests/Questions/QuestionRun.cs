using System.Text.Json;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;
using Aw.Judge.Host;
using Aw.Judge.Testing;
using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.Questions;

/// <summary>Asks a real question over in-memory fakes, with the package's JSON context and the default chain.</summary>
internal sealed class QuestionRun
{
    public static JsonSerializerOptions Json { get; } = AwJson.CreateOptions(JudgeJsonContext.Default);

    public InMemoryDecisionLog Log { get; } = new();

    public static ScriptedProvider Cli(params ContentClass[] classes) => new(ProviderName.ClaudeCli, classes);

    public static ScriptedProvider Rules(params ContentClass[] classes) => new(ProviderName.Rules, classes);

    public ValueTask<EvaluationOutcome> Ask<TInput>(QuestionDefinition<TInput> question, string input, params IModelProvider[] providers)
        where TInput : class
    {
        var evaluator = new QuestionEvaluator(Log, new FakeInputRedactor(), new SequentialIdGenerator(), new FakeTimeProvider(KnownInstants.Epoch), Json);
        return evaluator.Evaluate(question, input, new EvaluationContext { Providers = providers }, CancellationToken.None);
    }

    /// <summary>The outcome is a pre-rule's: the decision, by rules, confidence 1.</summary>
    public static void AssertPreRule(string decision, EvaluationOutcome outcome)
    {
        var decided = Assert.IsType<DecidedOutcome>(outcome);
        Assert.Equal((decision, ProviderName.Rules, "pre-rule", 1.0), (decided.Decision, decided.Model, decided.ReasonCode, decided.Confidence));
    }

    /// <summary>The outcome is a model's decision.</summary>
    public static DecidedOutcome AssertModel(string decision, EvaluationOutcome outcome)
    {
        var decided = Assert.IsType<DecidedOutcome>(outcome);
        Assert.Equal((decision, ProviderName.ClaudeCli, "model"), (decided.Decision, decided.Model, decided.ReasonCode));
        return decided;
    }

    /// <summary>The outcome is the question's fallback rule after an unsure model.</summary>
    public static void AssertFallback(string decision, EvaluationOutcome outcome)
    {
        var decided = Assert.IsType<DecidedOutcome>(outcome);
        Assert.Equal((decision, ProviderName.Rules, "fallback-after-undecided"), (decided.Decision, decided.Model, decided.ReasonCode));
    }
}
