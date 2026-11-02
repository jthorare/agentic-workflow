using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>
/// Carries config.json's providers.inputCapChars to Jev: a question that sets no cap of its own gets this one, so
/// <see cref="JevProvider"/> (one instance for every call) never reads the config itself.
/// </summary>
/// <param name="inner">The Jev adapter.</param>
/// <param name="capChars">The configured cap, in characters.</param>
internal sealed class JevInputCapProvider(IModelProvider inner, int capChars) : IModelProvider
{
    /// <inheritdoc />
    public ProviderName Name => inner.Name;

    /// <inheritdoc />
    public IReadOnlySet<ContentClass> Classes => inner.Classes;

    /// <inheritdoc />
    public ValueTask<ProviderResult> Decide(QuestionRef question, JsonNode? input, TimeSpan budget, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(question);
        return inner.Decide(question with { InputCapChars = question.InputCapChars ?? capChars }, input, budget, cancellationToken);
    }
}
