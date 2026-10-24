using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Config;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Testing;

/// <summary>
/// The IBatchClassifier fake; it passes BatchClassifierContract. It applies the same inclusion rule as the real adapter
/// (JevInclusion), so a test that excludes Jev gets 'unavailable' without the script being asked.
/// </summary>
/// <param name="script">What the (pretend) Jev answers to a batch.</param>
public sealed class FakeBatchClassifier(Func<BatchRequest, ProviderResult> script) : IBatchClassifier
{
    private readonly List<BatchRequest> _asked = [];

    /// <summary>The batches that reached the script, in order.</summary>
    public IReadOnlyList<BatchRequest> Asked => _asked;

    /// <summary>A classifier that answers every batch with these answers.</summary>
    /// <param name="answers">The answers by question key.</param>
    /// <returns>The fake.</returns>
    public static FakeBatchClassifier Answering(IReadOnlyDictionary<string, BatchAnswer> answers) => new(_ => new BatchDecided(answers));

    /// <summary>A classifier whose Jev is unavailable ('no-api-key').</summary>
    /// <returns>The fake.</returns>
    public static FakeBatchClassifier Unavailable() => new(_ => new ProviderUnavailable("no-api-key"));

    /// <inheritdoc />
    public ValueTask<ProviderResult> Classify(BatchRequest request, ProvidersConfig? providers, IReadOnlyCollection<ProviderName>? allowed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!JevInclusion.Of(providers, allowed))
        {
            return ValueTask.FromResult<ProviderResult>(new ProviderUnavailable("excluded"));
        }

        _asked.Add(request);
        return ValueTask.FromResult(script(request));
    }
}
