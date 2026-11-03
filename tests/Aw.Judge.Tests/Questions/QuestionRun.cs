using System.Text.Json;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;
using Aw.Judge.Host;
using Aw.Judge.Infrastructure.Images;
using Aw.Judge.Tests.Images;
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

    /// <summary>Reads the images a question names; by default an empty in-memory one (the integration tests use the real reader).</summary>
    public IImageReader ImageReader { get; init; } = new FakeImageReader();

    /// <summary>An in-memory reader that has these files (tiny PNGs) under the directory.</summary>
    public static FakeImageReader ReaderWith(string directory, params string[] names) =>
        new(names.ToDictionary(name => directory + "/" + name, _ => ImageBytes.Png(1, 1), StringComparer.Ordinal));

    public static ScriptedProvider Cli(params ContentClass[] classes) => new(ProviderName.ClaudeCli, classes);

    public static ScriptedProvider Rules(params ContentClass[] classes) => new(ProviderName.Rules, classes);

    public ValueTask<EvaluationOutcome> Ask<TInput>(QuestionDefinition<TInput> question, string input, params IModelProvider[] providers)
        where TInput : class
    {
        var evaluator = new QuestionEvaluator(Log, new FakeInputRedactor(), new SequentialIdGenerator(), new FakeTimeProvider(KnownInstants.Epoch), Json);
        return evaluator.Evaluate(question, input, new EvaluationContext { Providers = providers, ImageReader = ImageReader, ImageWorkspaces = new ImageWorkspaces() }, CancellationToken.None);
    }

    /// <summary>Asks a question by its catalog name (the path the CLI takes), so a per-call question builds its definition from the input.</summary>
    public ValueTask<EvaluationOutcome> AskByName(string name, string input, params IModelProvider[] providers) =>
        AskByName(name, input, entry: null, providers);

    /// <summary>Asks by catalog name with the question's config.json entry (<paramref name="entry"/>), when there is one.</summary>
    public ValueTask<EvaluationOutcome> AskByName(string name, string input, QuestionOverride? entry, params IModelProvider[] providers) =>
        AskByName(name, input, ChainSpec.Default, entry, providers);

    /// <summary>Asks by catalog name over an explicit chain.</summary>
    public ValueTask<EvaluationOutcome> AskByName(string name, string input, ChainSpec chain, params IModelProvider[] providers) =>
        AskByName(name, input, chain, entry: null, providers);

    private ValueTask<EvaluationOutcome> AskByName(string name, string input, ChainSpec chain, QuestionOverride? entry, IModelProvider[] providers)
    {
        var evaluator = new QuestionEvaluator(Log, new FakeInputRedactor(), new SequentialIdGenerator(), new FakeTimeProvider(KnownInstants.Epoch), Json);
        return evaluator.Ask(name, input, new EvaluationContext { Providers = providers, Chain = chain, Override = entry, ImageReader = ImageReader, ImageWorkspaces = new ImageWorkspaces() }, CancellationToken.None);
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
