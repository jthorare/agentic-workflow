using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Judge.Core.Decisions;
using Aw.Judge.Core.Evaluation;
using Aw.Judge.Core.Ids;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;
using Aw.Judge.Testing;
using Aw.Kernel.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Judge.Tests.Evaluation;

/// <summary>One evaluator over in-memory fakes, with the decision id it will use known up front.</summary>
internal sealed class EvaluatorHarness
{
    public EvaluatorHarness(InMemoryDecisionLog? log = null, IInputRedactor? redactor = null)
    {
        Log = log ?? new InMemoryDecisionLog();
        Evaluator = new QuestionEvaluator(Log, redactor ?? new FakeInputRedactor(), new SequentialIdGenerator(), Clock, JsonOptions);
    }

    /// <summary>The AwJson options over the test question's context.</summary>
    public static JsonSerializerOptions JsonOptions => TestJson.Options;

    /// <summary>The id the first evaluation gets.</summary>
    public static DecisionId FirstId { get; } = DecisionId.From(SequentialIdGenerator.IdAt(1).ToString());

    public FakeTimeProvider Clock { get; } = new(KnownInstants.Epoch);

    public InMemoryDecisionLog Log { get; }

    public QuestionEvaluator Evaluator { get; }

    /// <summary>The one decision row written.</summary>
    public DecisionRecord Row => Log.Decisions.Single();

    public static JsonNode Text(string text) => new JsonObject { ["text"] = text };

    public ValueTask<EvaluationOutcome> Run(QuestionDefinition<TextInput> question, JsonNode? input, params IModelProvider[] providers) =>
        Run(question, input, new EvaluationContext { Providers = providers });

    public ValueTask<EvaluationOutcome> Run(QuestionDefinition<TextInput> question, JsonNode? input, EvaluationContext context) =>
        Evaluator.Evaluate(question, input?.ToJsonString() ?? "null", context, CancellationToken.None);

    public ValueTask<EvaluationOutcome> RunText(QuestionDefinition<TextInput> question, string rawInput, params IModelProvider[] providers) =>
        Evaluator.Evaluate(question, rawInput, new EvaluationContext { Providers = providers }, CancellationToken.None);

    /// <summary>A chain that routes message-meta to exactly these providers.</summary>
    public static ChainSpec Chain(params ProviderName[] names) =>
        new(new Dictionary<ContentClass, IReadOnlyList<ProviderName>> { [ContentClass.MessageMeta] = names });
}
