using System.Text.Json.Nodes;
using Aw.Judge.Core.Batching;
using Aw.Judge.Core.Config;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Infrastructure.Traits;

/// <summary>ITraitJudge over the batched Jev call: the state is redacted here, and the call gets config.json's providers block (the defaults when the file is unreadable).</summary>
/// <param name="classifier">The batched Jev call.</param>
/// <param name="redactor">Removes secrets from the state.</param>
/// <param name="config">Where config.json is read.</param>
internal sealed class JevTraitJudge(IBatchClassifier classifier, IInputRedactor redactor, IJudgeConfigStore config) : ITraitJudge
{
    /// <inheritdoc />
    public async ValueTask<ProviderResult> Ask(JsonNode? state, IReadOnlyList<BatchQuestion> questions, TimeSpan budget, IReadOnlyCollection<ProviderName>? allowed, CancellationToken cancellationToken)
    {
        var reading = ConfigReading.From(await config.Load(cancellationToken).ConfigureAwait(false));
        var providers = reading.IsSuccess ? reading.Value.Config.Providers : null;
        return await classifier.Classify(new BatchRequest(redactor.Redact(state), questions, budget), providers, allowed, cancellationToken).ConfigureAwait(false);
    }
}
