using Aw.Judge.Features.LabelOutcomes;

namespace Aw.Judge.Testing;

/// <summary>Builds a LabelOutcomesRequest with deterministic defaults.</summary>
public sealed class LabelOutcomesRequestBuilder
{
    private readonly LabelOutcomesRequest _request = new();

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public LabelOutcomesRequest Build() => _request;
}
