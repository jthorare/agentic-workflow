using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Evaluation;

/// <summary>
/// The rules provider of a model-pick call: answers the first key, because the candidates are sent cheapest first, whenever the
/// chain reaches it. FallbackRules would not do: it only runs after a model answered below its threshold, not when Jev has no
/// key or times out.
/// </summary>
/// <param name="why">Why Jev did not decide, which becomes the answer's reason.</param>
internal sealed class ModelPickRules(ModelPickWhy why) : IModelProvider
{
    /// <inheritdoc />
    public ProviderName Name => ProviderName.Rules;

    /// <inheritdoc />
    public IReadOnlySet<ContentClass> Classes { get; } = new HashSet<ContentClass>(ContentClasses.All);

    /// <inheritdoc />
    public ValueTask<ProviderResult> Decide(QuestionRef question, JsonNode? input, TimeSpan budget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(question);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<ProviderResult>(question.Outputs.Count == 0
            ? new ProviderUnavailable(RulesProvider.NoRule)
            : new ProviderDecided(question.Outputs[0], 1.0, why.Reason ?? ModelPickWhy.RulesOnly));
    }
}
