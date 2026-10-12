using Aw.Judge.Features.GetConfig;

namespace Aw.Judge.Testing;

/// <summary>Builds a GetConfigRequest with deterministic defaults.</summary>
public sealed class GetConfigRequestBuilder
{
    private readonly GetConfigRequest _request = new();

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public GetConfigRequest Build() => _request;
}
