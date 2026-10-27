using Aw.Judge.Core.Providers;
using Aw.Judge.Features.Adjudicate;

namespace Aw.Judge.Testing;

/// <summary>Builds an AdjudicateRequest with deterministic defaults: 'ask-check', 60 items, every provider allowed.</summary>
public sealed class AdjudicateRequestBuilder
{
    private string _question = "ask-check";
    private int _limit = 60;
    private IReadOnlyList<ProviderName>? _providers;

    /// <summary>Sets the question.</summary>
    /// <param name="question">The question.</param>
    /// <returns>This builder.</returns>
    public AdjudicateRequestBuilder Question(string question)
    {
        _question = question;
        return this;
    }

    /// <summary>Sets how many items to try at most.</summary>
    /// <param name="limit">The limit.</param>
    /// <returns>This builder.</returns>
    public AdjudicateRequestBuilder Limit(int limit)
    {
        _limit = limit;
        return this;
    }

    /// <summary>Limits the providers (the --providers allowlist).</summary>
    /// <param name="providers">The allowed providers.</param>
    /// <returns>This builder.</returns>
    public AdjudicateRequestBuilder Allowing(params ProviderName[] providers)
    {
        _providers = providers;
        return this;
    }

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public AdjudicateRequest Build() => new(_question, _limit, _providers);
}
