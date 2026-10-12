using System.Text.Json.Nodes;
using Aw.Judge.Core.Providers;

namespace Aw.Judge.Core.Ports;

/// <summary>A model (or rule) provider in the chain: answers one question about one input.</summary>
public interface IModelProvider
{
    /// <summary>Which provider this is.</summary>
    public ProviderName Name { get; }

    /// <summary>The content classes it can judge.</summary>
    public IReadOnlySet<ContentClass> Classes { get; }

    /// <summary>
    /// Asks the question. Every outcome is a result: an answer within the question's outputs is
    /// <see cref="ProviderDecided"/>, one outside them is <see cref="ProviderErrored"/> ('out-of-enum'), a backend that fails
    /// is errored or <see cref="ProviderUnavailable"/>, and a provider that runs past <paramref name="budget"/> stops
    /// and is unavailable with reason <see cref="ProviderUnavailable.Timeout"/>. It never throws, except
    /// OperationCanceledException for <paramref name="cancellationToken"/> (ModelProviderContract).
    /// </summary>
    /// <param name="question">The question, with the prompt built for this input.</param>
    /// <param name="input">The validated input (JSON null as null).</param>
    /// <param name="budget">The question's time budget.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The answer.</returns>
    public ValueTask<ProviderResult> Decide(QuestionRef question, JsonNode? input, TimeSpan budget, CancellationToken cancellationToken);
}
