using System.Text.Json.Nodes;
using Aw.Judge.Core.Ports;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Infrastructure.Providers;

/// <summary>
/// The boundary backstop the catalog puts around each adapter: IModelProvider's contract says Decide never throws
/// except for the caller's cancellation, so a throw that gets through anyway is an adapter bug, answered as errored
/// ('provider-threw', a failure row) so the chain moves on and the hook still gets an answer.
/// </summary>
/// <param name="inner">The adapter.</param>
internal sealed class FailOpenModelProvider(IModelProvider inner) : IModelProvider
{
    /// <summary>The adapter it wraps.</summary>
    public IModelProvider Inner { get; } = inner;

    /// <inheritdoc />
    public ProviderName Name => Inner.Name;

    /// <inheritdoc />
    public IReadOnlySet<ContentClass> Classes => Inner.Classes;

    /// <inheritdoc />
    public async ValueTask<ProviderResult> Decide(QuestionRef question, JsonNode? input, TimeSpan budget, CancellationToken cancellationToken)
    {
        try
        {
            return await Inner.Decide(question, input, budget, cancellationToken).ConfigureAwait(false);
        }
        // The boundary backstop (CA1031 is off for Infrastructure): any adapter bug becomes a recorded 'provider-threw'
        // failure; the caller's cancellation still propagates.
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new ProviderErrored(ProviderReasons.ProviderThrew);
        }
    }
}
