using Aw.Judge.Features.GetLabelStatus;

namespace Aw.Judge.Testing;

/// <summary>Builds a GetLabelStatusRequest with deterministic defaults.</summary>
public sealed class GetLabelStatusRequestBuilder
{
    private readonly GetLabelStatusRequest _request = new();

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public GetLabelStatusRequest Build() => _request;
}
