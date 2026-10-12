using Aw.Judge.Features.GetHealth;

namespace Aw.Judge.Testing;

/// <summary>Builds a GetHealthRequest with deterministic defaults.</summary>
public sealed class GetHealthRequestBuilder
{
    private readonly GetHealthRequest _request = new();

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public GetHealthRequest Build() => _request;
}
