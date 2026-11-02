using System.Text;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;
using Aw.Judge.Core.Questions;
using Aw.Kernel.Json;

namespace Aw.Judge.Core.Evaluation;

/// <summary>model-pick's entry: its definition comes from the input, the pack filters run before the chain, and the chain is fixed.</summary>
public sealed partial class QuestionEvaluator
{
    /// <summary>
    /// Evaluates a model-pick call. The chain is Jev then rules whatever the context lists: the agent CLIs never see the prompt, a
    /// question entry in config.json or --providers can only narrow it, and the rules answer the cheapest key.
    /// </summary>
    /// <param name="rawInput">The input text as read; input that doesn't deserialize escalates as 'invalid-input'.</param>
    /// <param name="context">The providers, the chain and the question's config.</param>
    /// <param name="cancellationToken">Cancels the provider calls and the writes.</param>
    /// <returns>The decision, or an escalation.</returns>
    public ValueTask<EvaluationOutcome> EvaluateModelPick(string rawInput, EvaluationContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rawInput);
        ArgumentNullException.ThrowIfNull(context);
        var parsed = AwJson.Deserialize<ModelPickInput>(Encoding.UTF8.GetBytes(rawInput), json);
        var question = parsed.IsError ? ModelPickQuestion.Unreadable : ModelPickQuestion.For(parsed.Value);
        return Evaluate(question, rawInput, FixedChain(context), cancellationToken);
    }

    // Jev when the context's chain has it and a Jev provider is present, then the rules; no other provider is passed on.
    private static EvaluationContext FixedChain(EvaluationContext context)
    {
        var why = new ModelPickWhy();
        var threshold = context.Override?.Threshold ?? ModelPickQuestion.DefaultThreshold;
        List<IModelProvider> providers = [];
        List<ProviderName> names = [];
        var jev = context.Providers.FirstOrDefault(static p => p.Name == ProviderName.Jev);
        if (jev is not null && context.Chain.For(ContentClass.Brief).Contains(ProviderName.Jev))
        {
            providers.Add(new ModelPickJev(jev, threshold, why));
            names.Add(ProviderName.Jev);
        }

        providers.Add(new ModelPickRules(why));
        names.Add(ProviderName.Rules);
        return context with
        {
            Providers = providers,
            Chain = new ChainSpec(new Dictionary<ContentClass, IReadOnlyList<ProviderName>> { [ContentClass.Brief] = names }),
        };
    }
}
