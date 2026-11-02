using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Config;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;

namespace Aw.Sindri.Tests.ContextCalls.Routing;

/// <summary>Jev's batch classifier from a script: records every batch (and the providers config it came with), answers with what the script says.</summary>
/// <param name="script">What the pretend Jev answers to a batch.</param>
internal sealed class ScriptedBatches(Func<BatchRequest, ProviderResult> script) : IBatchClassifier
{
    public List<BatchRequest> Asked { get; } = [];

    public List<ProvidersConfig?> Configs { get; } = [];

    // Jev picks the last option of every choice question, at 0.9.
    public static ScriptedBatches Picking(double confidence = 0.9) => new(request => new BatchDecided(request.Questions.OfType<ChoiceBatchQuestion>().ToDictionary(
        static q => q.Key, q => (BatchAnswer)new ChoiceBatchAnswer(q.Criteria.Keys.Last(), confidence, new Dictionary<string, double>(StringComparer.Ordinal)), StringComparer.Ordinal)));

    public ValueTask<ProviderResult> Classify(BatchRequest request, ProvidersConfig? providers, IReadOnlyCollection<ProviderName>? allowed, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Asked.Add(request);
        Configs.Add(providers);
        return ValueTask.FromResult(script(request));
    }
}
