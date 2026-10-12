using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Evaluation;

/// <summary>
/// The 'rules' provider: local and deterministic, always the chain's last resort. It has no rule table of its own (each
/// question's pre-rules run before the chain and its fallback rule settles a below-threshold answer), so it is always
/// unavailable: a chain of rules alone escalates any input no pre-rule settles, with no failure recorded.
/// </summary>
public sealed class RulesProvider : IModelProvider
{
    /// <summary>The reason it gives.</summary>
    public const string NoRule = "no-rules-fallback-configured";

    private static readonly ProviderUnavailable _unavailable = new(NoRule);

    /// <inheritdoc />
    public ProviderName Name => ProviderName.Rules;

    /// <inheritdoc />
    public IReadOnlySet<ContentClass> Classes { get; } = new HashSet<ContentClass>(ContentClasses.All);

    /// <inheritdoc />
    public ValueTask<ProviderResult> Decide(QuestionRef question, JsonNode? input, TimeSpan budget, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<ProviderResult>(_unavailable);
    }
}
