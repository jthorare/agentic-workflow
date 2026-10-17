using Aw.Sindri.Features.GetLaunchTargets;

namespace Aw.Sindri.Testing;

/// <summary>Builds a GetLaunchTargetsRequest with deterministic defaults.</summary>
public sealed class GetLaunchTargetsRequestBuilder
{
    private readonly GetLaunchTargetsRequest _request = new();

    /// <summary>Builds the request.</summary>
    /// <returns>The request.</returns>
    public GetLaunchTargetsRequest Build() => _request;
}
