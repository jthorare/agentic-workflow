using Aw.Sindri.Features.GetNeedsYou;

namespace Aw.Sindri.Testing;

/// <summary>Builds a GetNeedsYouRequest with deterministic defaults.</summary>
public sealed class GetNeedsYouRequestBuilder
{
    private readonly GetNeedsYouRequest _request = new();

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public GetNeedsYouRequest Build() => _request;
}
